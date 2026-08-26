using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Minio;
using Minio.DataModel.Args;
using Vokasia.Api.Auth;
using Vokasia.Api.Authorization;
using Vokasia.Api.Storage;
using Vokasia.Api.Validation;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Api.Endpoints;

/// <summary>
/// V3.1 Manual Billing MVP:
/// - TenantAdmin: view subscription, list invoices, view bank transfer instructions, presigned proof upload, submit proof, download proof.
/// - SuperAdmin: list all invoices, filter by status/tenant, view pending verification queue, view proof, approve payment, reject payment.
/// - Full transactional safety, idempotency, and audit logging.
/// </summary>
public static class BillingEndpoints
{
    public static IEndpointRouteBuilder MapBillingEndpoints(this IEndpointRouteBuilder app)
    {
        // SuperAdmin Routes
        var sa = app.MapGroup("/sa/invoices").WithTags("SaBilling")
            .RequireAuthorization(RbacPolicies.SaOnly)
            .AddEndpointFilter<ValidationFilter>();

        sa.MapGet("/", ListAllInvoices);
        sa.MapGet("/{id:guid}", GetInvoiceDetailSa);
        sa.MapGet("/{id:guid}/payment-proof/download-url", GetPaymentProofDownloadUrlSa);
        sa.MapPost("/{id:guid}/confirm-payment", ConfirmPayment);
        sa.MapPost("/{id:guid}/reject-payment", RejectPayment);

        // TenantAdmin Routes
        var tenantGroup = app.MapGroup("/api/invoices").WithTags("Billing")
            .RequireAuthorization(RbacPolicies.TenantAdminOnly)
            .AddEndpointFilter<ValidationFilter>();

        tenantGroup.MapGet("/", ListMyInvoices);
        tenantGroup.MapGet("/subscription", GetMySubscription);
        tenantGroup.MapGet("/bank-instructions", GetBankInstructions);
        tenantGroup.MapGet("/{id:guid}", GetMyInvoiceDetail);
        tenantGroup.MapGet("/{id:guid}/payment-proof/download-url", GetPaymentProofDownloadUrlTenant);
        tenantGroup.MapPost("/{id:guid}/payment-proof/upload-url", GetPaymentProofUploadUrl);
        tenantGroup.MapPost("/{id:guid}/payment-proof", UploadPaymentProof);

        return app;
    }

    private static async Task<IResult> ListAllInvoices(
        VokasiaDbContext db,
        CancellationToken ct,
        [FromQuery] Guid? tenantId = null,
        [FromQuery] InvoiceStatus? status = null)
    {
        var query = db.Invoices.AsNoTracking().AsQueryable();
        if (tenantId.HasValue)
        {
            query = query.Where(i => i.TenantId == tenantId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(i => i.Status == status.Value);
        }

        var items = await query.OrderByDescending(i => i.IssuedAt).Select(i => ToDto(i)).ToListAsync(ct);
        return Results.Ok(items);
    }

    private static async Task<IResult> GetInvoiceDetailSa(
        Guid id,
        VokasiaDbContext db,
        CancellationToken ct)
    {
        var invoice = await db.Invoices.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, ct);
        if (invoice is null)
        {
            return Results.NotFound();
        }

        var tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == invoice.TenantId, ct);
        var submissions = await db.PaymentSubmissions.AsNoTracking()
            .Where(s => s.InvoiceId == id)
            .OrderByDescending(s => s.SubmittedAt)
            .Select(s => ToSubmissionDto(s))
            .ToListAsync(ct);

        return Results.Ok(new
        {
            Invoice = ToDto(invoice),
            SchoolName = tenant?.SchoolName ?? "Sekolah",
            Submissions = submissions,
        });
    }

    private static async Task<IResult> GetPaymentProofDownloadUrlSa(
        Guid id,
        VokasiaDbContext db,
        IBrowserObjectStorageSigner storageSigner,
        IConfiguration config,
        CancellationToken ct)
    {
        var invoice = await db.Invoices.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, ct);
        if (invoice is null || string.IsNullOrEmpty(invoice.ProofKey))
        {
            return Results.NotFound();
        }

        var expirySeconds = 300;
        var url = await storageSigner.PresignedGetObjectAsync(new PresignedGetObjectArgs()
            .WithBucket(config["Minio:Bucket"] ?? "vokasia-journal")
            .WithObject(invoice.ProofKey)
            .WithExpiry(expirySeconds));

        return Results.Ok(new { DownloadUrl = url, ObjectKey = invoice.ProofKey, ExpiresIn = expirySeconds });
    }

    /// <summary>
    /// Transactional SuperAdmin Approval:
    /// - Checks Status == PendingVerification
    /// - Sets Invoice.Status = Paid, PaidAt = UtcNow
    /// - Updates PaymentSubmission as Approved
    /// - Creates or extends active Subscription via BillingRules.CalculateSubscriptionDates
    /// - Writes AuditLog
    /// </summary>
    private static async Task<IResult> ConfirmPayment(
        Guid id,
        VokasiaDbContext db,
        ITenantContext actingUser,
        CancellationToken ct)
    {
        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (invoice is null)
        {
            return Results.NotFound();
        }

        if (invoice.Status != InvoiceStatus.PendingVerification)
        {
            return Results.Conflict(new { message = "Invoice tidak dalam status menunggu verifikasi pembayaran." });
        }

        var now = DateTimeOffset.UtcNow;
        invoice.Status = InvoiceStatus.Paid;
        invoice.PaidAt = now;
        invoice.RejectionReason = null;

        // Update latest pending submission
        var submission = await db.PaymentSubmissions
            .Where(s => s.InvoiceId == id && s.Approved == null)
            .OrderByDescending(s => s.SubmittedAt)
            .FirstOrDefaultAsync(ct);

        if (submission != null)
        {
            submission.Approved = true;
            submission.VerifiedBy = actingUser.UserId;
            submission.VerifiedAt = now;
        }

        // Calculate and apply subscription
        var existingSub = await db.Subscriptions.FirstOrDefaultAsync(s => s.TenantId == invoice.TenantId, ct);
        var (startsAt, endsAt) = BillingRules.CalculateSubscriptionDates(
            now,
            existingSub?.StartsAt,
            existingSub?.EndsAt,
            existingSub?.Status);

        if (existingSub == null)
        {
            existingSub = new Subscription
            {
                Id = Guid.NewGuid(),
                TenantId = invoice.TenantId,
                PlanId = invoice.PlanId,
                PlanNameSnapshot = invoice.PlanNameSnapshot,
                StartsAt = startsAt,
                EndsAt = endsAt,
                Status = SubscriptionStatus.Active,
                SourceInvoiceId = invoice.Id,
                CreatedAt = now,
                UpdatedAt = now,
            };
            db.Subscriptions.Add(existingSub);
        }
        else
        {
            existingSub.PlanId = invoice.PlanId;
            existingSub.PlanNameSnapshot = invoice.PlanNameSnapshot;
            existingSub.StartsAt = startsAt;
            existingSub.EndsAt = endsAt;
            existingSub.Status = SubscriptionStatus.Active;
            existingSub.SourceInvoiceId = invoice.Id;
            existingSub.UpdatedAt = now;
        }

        // Ensure Tenant is marked active
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == invoice.TenantId, ct);
        if (tenant != null)
        {
            tenant.IsActive = true;
            tenant.PlanId = invoice.PlanId;
        }

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = invoice.TenantId,
            ActorUserId = actingUser.UserId ?? Guid.Empty,
            Action = "InvoicePaymentConfirmed",
            Entity = nameof(Invoice),
            EntityId = invoice.Id.ToString(),
            MetaJson = JsonSerializer.Serialize(new
            {
                invoice.InvoiceNumber,
                invoice.PeriodMonth,
                invoice.AmountSnapshot,
                SubscriptionStartsAt = startsAt,
                SubscriptionEndsAt = endsAt,
            }),
        });

        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    /// <summary>
    /// Transactional SuperAdmin Rejection:
    /// - Checks Status == PendingVerification
    /// - Sets Invoice.Status = Rejected, RejectionReason = req.Reason
    /// - Updates PaymentSubmission as Rejected with VerificationReason
    /// - Writes AuditLog
    /// </summary>
    private static async Task<IResult> RejectPayment(
        Guid id,
        RejectPaymentRequest req,
        VokasiaDbContext db,
        ITenantContext actingUser,
        CancellationToken ct)
    {
        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (invoice is null)
        {
            return Results.NotFound();
        }

        if (invoice.Status != InvoiceStatus.PendingVerification)
        {
            return Results.Conflict(new { message = "Invoice tidak dalam status menunggu verifikasi pembayaran." });
        }

        var now = DateTimeOffset.UtcNow;
        invoice.Status = InvoiceStatus.Rejected;
        invoice.RejectionReason = req.Reason;

        var submission = await db.PaymentSubmissions
            .Where(s => s.InvoiceId == id && s.Approved == null)
            .OrderByDescending(s => s.SubmittedAt)
            .FirstOrDefaultAsync(ct);

        if (submission != null)
        {
            submission.Approved = false;
            submission.VerifiedBy = actingUser.UserId;
            submission.VerifiedAt = now;
            submission.VerificationReason = req.Reason;
        }

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = invoice.TenantId,
            ActorUserId = actingUser.UserId ?? Guid.Empty,
            Action = "InvoicePaymentRejected",
            Entity = nameof(Invoice),
            EntityId = invoice.Id.ToString(),
            MetaJson = JsonSerializer.Serialize(new
            {
                invoice.InvoiceNumber,
                invoice.PeriodMonth,
                Reason = req.Reason,
            }),
        });

        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ListMyInvoices(
        VokasiaDbContext db,
        ITenantContext tenant,
        CancellationToken ct)
    {
        if (!tenant.TenantId.HasValue)
        {
            return Results.Forbid();
        }

        var items = await db.Invoices.AsNoTracking()
            .Where(i => i.TenantId == tenant.TenantId.Value)
            .OrderByDescending(i => i.IssuedAt)
            .Select(i => ToDto(i))
            .ToListAsync(ct);

        return Results.Ok(items);
    }

    private static async Task<IResult> GetMySubscription(
        VokasiaDbContext db,
        ITenantContext tenant,
        CancellationToken ct)
    {
        if (!tenant.TenantId.HasValue)
        {
            return Results.Forbid();
        }

        var subscription = await db.Subscriptions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenant.TenantId.Value, ct);

        if (subscription is null)
        {
            return Results.NotFound(new { message = "Langganan belum terdaftar." });
        }

        var plan = await db.Plans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == subscription.PlanId, ct);
        var now = DateTimeOffset.UtcNow;
        var effectiveStatus = BillingRules.IsSubscriptionActive(subscription.Status, subscription.EndsAt, now)
            ? SubscriptionStatus.Active
            : (subscription.Status == SubscriptionStatus.Active ? SubscriptionStatus.Expired : subscription.Status);

        return Results.Ok(new SubscriptionDto(
            subscription.Id,
            subscription.TenantId,
            subscription.PlanId,
            subscription.PlanNameSnapshot,
            subscription.StartsAt,
            subscription.EndsAt,
            effectiveStatus,
            plan?.MaxStudents ?? 0,
            plan?.PriceAnnual ?? 0m));
    }

    private static IResult GetBankInstructions(IConfiguration config)
    {
        var bankName = config["Billing:BankName"] ?? "Bank Central Asia (BCA)";
        var accountNumber = config["Billing:AccountNumber"] ?? "8830192831";
        var accountHolder = config["Billing:AccountHolder"] ?? "PT Vokasia Media Indonesia";

        return Results.Ok(new BankTransferInstructionsDto(bankName, accountNumber, accountHolder));
    }

    private static async Task<IResult> GetMyInvoiceDetail(
        Guid id,
        VokasiaDbContext db,
        ITenantContext tenant,
        CancellationToken ct)
    {
        if (!tenant.TenantId.HasValue)
        {
            return Results.Forbid();
        }

        var invoice = await db.Invoices.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id && i.TenantId == tenant.TenantId.Value, ct);

        if (invoice is null)
        {
            return Results.NotFound();
        }

        var submissions = await db.PaymentSubmissions.AsNoTracking()
            .Where(s => s.InvoiceId == id && s.TenantId == tenant.TenantId.Value)
            .OrderByDescending(s => s.SubmittedAt)
            .Select(s => ToSubmissionDto(s))
            .ToListAsync(ct);

        return Results.Ok(new
        {
            Invoice = ToDto(invoice),
            Submissions = submissions,
        });
    }

    private static async Task<IResult> GetPaymentProofDownloadUrlTenant(
        Guid id,
        VokasiaDbContext db,
        ITenantContext tenant,
        IBrowserObjectStorageSigner storageSigner,
        IConfiguration config,
        CancellationToken ct)
    {
        if (!tenant.TenantId.HasValue)
        {
            return Results.Forbid();
        }

        var invoice = await db.Invoices.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id && i.TenantId == tenant.TenantId.Value, ct);

        if (invoice is null || string.IsNullOrEmpty(invoice.ProofKey))
        {
            return Results.NotFound();
        }

        var expirySeconds = 300;
        var url = await storageSigner.PresignedGetObjectAsync(new PresignedGetObjectArgs()
            .WithBucket(config["Minio:Bucket"] ?? "vokasia-journal")
            .WithObject(invoice.ProofKey)
            .WithExpiry(expirySeconds));

        return Results.Ok(new { DownloadUrl = url, ObjectKey = invoice.ProofKey, ExpiresIn = expirySeconds });
    }

    private static async Task<IResult> GetPaymentProofUploadUrl(
        Guid id,
        PaymentProofUploadRequest req,
        VokasiaDbContext db,
        ITenantContext tenant,
        IBrowserObjectStorageSigner storageSigner,
        IConfiguration config,
        CancellationToken ct)
    {
        if (!tenant.TenantId.HasValue)
        {
            return Results.Forbid();
        }

        if (req.ContentType is not ("image/jpeg" or "image/png" or "application/pdf") || req.SizeBytes is < 1 or > 10_000_000)
        {
            return Results.BadRequest(new { message = "Bukti harus JPG, PNG, atau PDF dan maksimal 10 MB." });
        }

        var invoice = await db.Invoices.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id && i.TenantId == tenant.TenantId.Value, ct);

        if (invoice is null)
        {
            return Results.NotFound();
        }

        if (invoice.Status != InvoiceStatus.Unpaid && invoice.Status != InvoiceStatus.Rejected)
        {
            return Results.Conflict(new { message = "Invoice tidak dalam status yang dapat mengunggah bukti pembayaran." });
        }

        var extension = req.ContentType switch { "image/jpeg" => "jpg", "image/png" => "png", _ => "pdf" };
        var objectKey = $"tenant/{tenant.TenantId.Value}/invoices/{id}/{Guid.NewGuid():N}.{extension}";
        var expirySeconds = 300;

        var url = await storageSigner.PresignedPutObjectAsync(new PresignedPutObjectArgs()
            .WithBucket(config["Minio:Bucket"] ?? "vokasia-journal")
            .WithObject(objectKey)
            .WithExpiry(expirySeconds));

        return Results.Ok(new PresignedUploadDto(url, objectKey, expirySeconds));
    }

    private static async Task<IResult> UploadPaymentProof(
        Guid id,
        UploadPaymentProofRequest req,
        VokasiaDbContext db,
        ITenantContext tenant,
        CancellationToken ct)
    {
        if (!tenant.TenantId.HasValue)
        {
            return Results.Forbid();
        }

        var invoice = await db.Invoices
            .FirstOrDefaultAsync(i => i.Id == id && i.TenantId == tenant.TenantId.Value, ct);

        if (invoice is null)
        {
            return Results.NotFound();
        }

        if (invoice.Status != InvoiceStatus.Unpaid && invoice.Status != InvoiceStatus.Rejected)
        {
            return Results.Conflict(new { message = "Invoice tidak dalam status yang dapat mengajukan pembayaran." });
        }

        if (!ObjectStorageKeyPolicy.IsOwnedKey(req.ObjectKey, tenant.TenantId.Value, $"invoices/{id:D}"))
        {
            return Results.BadRequest(new { message = "ObjectKey bukti pembayaran harus berada di ruang invoice tenant ini." });
        }

        var now = DateTimeOffset.UtcNow;
        invoice.ProofKey = req.ObjectKey;
        invoice.Status = InvoiceStatus.PendingVerification;

        var submission = new PaymentSubmission
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.TenantId.Value,
            InvoiceId = invoice.Id,
            SubmittedBy = tenant.UserId ?? Guid.Empty,
            SubmittedAt = now,
            ProofKey = req.ObjectKey,
            Note = req.Note,
        };
        db.PaymentSubmissions.Add(submission);

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.TenantId.Value,
            ActorUserId = tenant.UserId ?? Guid.Empty,
            Action = "PaymentProofSubmitted",
            Entity = nameof(Invoice),
            EntityId = invoice.Id.ToString(),
            MetaJson = JsonSerializer.Serialize(new
            {
                invoice.InvoiceNumber,
                SubmissionId = submission.Id,
                req.Note,
            }),
        });

        await db.SaveChangesAsync(ct);
        return Results.Ok(ToDto(invoice));
    }

    private static InvoiceDto ToDto(Invoice i)
    {
        var issued = i.IssuedAt.Year < 2000 ? DateTimeOffset.UtcNow : i.IssuedAt;
        var due = i.DueAt.Year < 2000 ? issued.AddDays(30) : i.DueAt;
        return new(
            i.Id,
            i.TenantId,
            string.IsNullOrEmpty(i.InvoiceNumber) ? $"VOK-{i.PeriodMonth.Year}-{i.Id.ToString()[..6].ToUpper()}" : i.InvoiceNumber,
            string.IsNullOrEmpty(i.PlanNameSnapshot) ? "Paket Tahunan" : i.PlanNameSnapshot,
            i.AmountSnapshot,
            i.StudentCapacitySnapshot == 0 ? 500 : i.StudentCapacitySnapshot,
            i.PeriodMonth,
            issued,
            due,
            i.PaidAt,
            i.Status,
            i.ProofKey,
            i.RejectionReason);
    }
    private static PaymentSubmissionDto ToSubmissionDto(PaymentSubmission s) => new(
        s.Id,
        s.InvoiceId,
        s.SubmittedBy,
        s.SubmittedAt,
        s.ProofKey,
        s.Note,
        s.Approved,
        s.VerifiedAt,
        s.VerificationReason);
}
