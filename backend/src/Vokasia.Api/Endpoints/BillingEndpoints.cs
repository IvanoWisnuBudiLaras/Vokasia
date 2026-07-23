using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vokasia.Api.Auth;
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

    /// <summary>AC: "bukti transfer via presigned (FR-BIL-02); status ProofUploaded." objectKey diasumsikan sudah diupload klien lewat presigned PUT URL (pola sama Visit.SignatureKey/PhotoKey — endpoint ini hanya mencatat referensinya, bukan menerima file mentah).</summary>
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

        invoice.ProofKey = req.ObjectKey;
        invoice.Status = InvoiceStatus.ProofUploaded;
        await db.SaveChangesAsync(ct);

        return Results.Ok(ToDto(invoice));
    }

    private static InvoiceDto ToDto(Invoice i) => new(i.Id, i.TenantId, i.PeriodMonth, i.Amount, i.Status, i.ProofKey);
}
