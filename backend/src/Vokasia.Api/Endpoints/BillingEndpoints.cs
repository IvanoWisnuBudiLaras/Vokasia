using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Minio;
using Minio.DataModel.Args;
using Vokasia.Api.Auth;
using Vokasia.Api.Storage;
using Vokasia.Api.Validation;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Api.Endpoints;

/// <summary>
/// VOK-H6-E1 §5 — Billing (FR-BIL-01..03). Dua permukaan sesuai AC ticket ("SA semua / TenantAdmin
/// miliknya" — pola SAMA dgn QueryAuditLogs §4): /sa/invoices (SaOnly, semua tenant + ConfirmPayment)
/// dan /api/invoices (TenantAdminOnly, tenant sendiri saja + UploadPaymentProof). GenerateMonthlyInvoices
/// & CheckQuotaOnPlacement ada di BillingCronJobs (Worker) & CompaniesAndPlacementsEndpoints - bukan di sini.
/// </summary>
public static class BillingEndpoints
{
    public static IEndpointRouteBuilder MapBillingEndpoints(this IEndpointRouteBuilder app)
    {
        var sa = app.MapGroup("/sa/invoices").WithTags("SaBilling").RequireAuthorization(RbacPolicies.SaOnly);
        sa.MapGet("/", ListAllInvoices);
        sa.MapPost("/{id:guid}/confirm-payment", ConfirmPayment);

        var tenantGroup = app.MapGroup("/api/invoices").WithTags("Billing")
            .RequireAuthorization(RbacPolicies.TenantAdminOnly)
            .AddEndpointFilter<ValidationFilter>();
        tenantGroup.MapGet("/", ListMyInvoices);
        tenantGroup.MapPost("/{id:guid}/payment-proof/upload-url", GetPaymentProofUploadUrl);
        tenantGroup.MapPost("/{id:guid}/payment-proof", UploadPaymentProof);

        return app;
    }

    private static async Task<IResult> ListAllInvoices(VokasiaDbContext db, CancellationToken ct, [FromQuery] Guid? tenantId = null)
    {
        var query = db.Invoices.AsNoTracking().AsQueryable();
        if (tenantId.HasValue)
        {
            query = query.Where(i => i.TenantId == tenantId.Value);
        }

        var items = await query.OrderByDescending(i => i.PeriodMonth).Select(i => ToDto(i)).ToListAsync(ct);
        return Results.Ok(items);
    }

    /// <summary>AC: "Paid + audit; tolak jika tanpa bukti."</summary>
    private static async Task<IResult> ConfirmPayment(Guid id, VokasiaDbContext db, ITenantContext actingUser, CancellationToken ct)
    {
        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (invoice is null)
        {
            return Results.NotFound();
        }

        if (string.IsNullOrEmpty(invoice.ProofKey))
        {
            return Results.Conflict(new { message = "Invoice belum ada bukti bayar (ProofKey kosong) — tak bisa dikonfirmasi lunas." });
        }

        invoice.Status = InvoiceStatus.Paid;

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = invoice.TenantId,
            ActorUserId = actingUser.UserId ?? Guid.Empty,
            Action = "InvoicePaymentConfirmed",
            Entity = nameof(Invoice),
            EntityId = invoice.Id.ToString(),
            MetaJson = JsonSerializer.Serialize(new { invoice.PeriodMonth, invoice.Amount }),
        });

        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ListMyInvoices(VokasiaDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        if (!tenant.TenantId.HasValue)
        {
            return Results.Forbid();
        }

        var items = await db.Invoices.AsNoTracking().Where(i => i.TenantId == tenant.TenantId)
            .OrderByDescending(i => i.PeriodMonth).Select(i => ToDto(i)).ToListAsync(ct);
        return Results.Ok(items);
    }

    private static async Task<IResult> GetPaymentProofUploadUrl(
        Guid id, PaymentProofUploadRequest req, VokasiaDbContext db, ITenantContext tenant,
        IBrowserObjectStorageSigner storageSigner,
        IConfiguration config, CancellationToken ct)
    {
        if (!tenant.TenantId.HasValue)
        {
            return Results.Forbid();
        }
        if (req.ContentType is not ("image/jpeg" or "image/png" or "application/pdf") || req.SizeBytes is < 1 or > 10_000_000)
        {
            return Results.BadRequest(new { message = "Bukti harus JPG, PNG, atau PDF dan maksimal 10 MB." });
        }
        var invoice = await db.Invoices.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id && i.TenantId == tenant.TenantId, ct);
        if (invoice is null)
        {
            return Results.NotFound();
        }
        var extension = req.ContentType switch { "image/jpeg" => "jpg", "image/png" => "png", _ => "pdf" };
        var objectKey = $"tenant/{tenant.TenantId}/invoices/{id}/{Guid.NewGuid():N}.{extension}";
        var expirySeconds = 300;
        var url = await storageSigner.PresignedPutObjectAsync(new PresignedPutObjectArgs()
            .WithBucket(config["Minio:Bucket"] ?? "vokasia-journal")
            .WithObject(objectKey)
            .WithExpiry(expirySeconds));
        return Results.Ok(new PresignedUploadDto(url, objectKey, expirySeconds));
    }

    /// <summary>Attach only a backend-generated, tenant-scoped object key after the presigned upload succeeds.</summary>
    private static async Task<IResult> UploadPaymentProof(Guid id, UploadPaymentProofRequest req, VokasiaDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        if (!tenant.TenantId.HasValue)
        {
            return Results.Forbid();
        }

        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == id && i.TenantId == tenant.TenantId, ct);
        if (invoice is null)
        {
            return Results.NotFound();
        }

        if (!ObjectStorageKeyPolicy.IsOwnedKey(req.ObjectKey, tenant.TenantId.Value, "invoices"))
        {
            return Results.BadRequest(new { message = "ObjectKey bukti pembayaran harus berada di ruang penyimpanan tenant ini." });
        }

        invoice.ProofKey = req.ObjectKey;
        invoice.Status = InvoiceStatus.ProofUploaded;
        await db.SaveChangesAsync(ct);

        return Results.Ok(ToDto(invoice));
    }

    private static InvoiceDto ToDto(Invoice i) => new(i.Id, i.TenantId, i.PeriodMonth, i.Amount, i.Status, i.ProofKey);
}
