using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Worker.Jobs;

/// <summary>
/// VOK-H6-E1 §5 — GenerateMonthlyInvoices, cron tgl 1 02:00 WIB (didaftar Worker/Program.cs, pola
/// SAMA persis dgn AssessmentCronJobs/JournalCronJobs: DbContext tanpa AmbientTenantContext -> lintas
/// semua tenant by design). Idempoten per (tenant,bulan) DIJAMIN GANDA: (1) query WHERE tak
/// mengulang tenant yang SUDAH punya Invoice bulan itu (idempoten by construction, sama filosofi
/// AssessmentCronJobs.OpenAssessmentPhase), DAN (2) unique index {TenantId,PeriodMonth}
/// (VokasiaDbContext) sbg jaring pengaman KEDUA kalau ada race — AC ticket literal: "cron invoice 2x
/// di bulan sama, Then invoice tetap 1 per tenant".
/// </summary>
public class BillingCronJobs(VokasiaDbContext db, ILogger<BillingCronJobs> logger)
{
    public async Task GenerateMonthlyInvoices(DateOnly? runDate = null)
    {
        var today = runDate ?? AppTimeZone.TodayJakarta();
        var periodMonth = new DateOnly(today.Year, today.Month, 1);

        var activeTenants = await db.Tenants.AsNoTracking()
            .Where(t => t.IsActive && t.PlanId.HasValue)
            .Select(t => new { t.Id, t.SchoolName, t.PlanId })
            .ToListAsync();

        if (activeTenants.Count == 0)
        {
            logger.LogInformation("GenerateMonthlyInvoices: {Month} tak ada tenant aktif berplan.", periodMonth);
            return;
        }

        var tenantIds = activeTenants.Select(t => t.Id).ToList();
        var alreadyInvoiced = (await db.Invoices.AsNoTracking()
            .Where(i => tenantIds.Contains(i.TenantId) && i.PeriodMonth == periodMonth)
            .Select(i => i.TenantId)
            .ToListAsync())
            .ToHashSet();

        var toInvoice = activeTenants.Where(t => !alreadyInvoiced.Contains(t.Id)).ToList();
        if (toInvoice.Count == 0)
        {
            logger.LogInformation("GenerateMonthlyInvoices: {Month} semua tenant aktif sudah punya invoice bulan ini (idempoten, nol baru).", periodMonth);
            return;
        }

        var planIds = toInvoice.Select(t => t.PlanId!.Value).Distinct().ToList();
        var plans = await db.Plans.AsNoTracking().Where(p => planIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.PriceMonthly);

        foreach (var t in toInvoice)
        {
            if (!plans.TryGetValue(t.PlanId!.Value, out var price))
            {
                logger.LogWarning("GenerateMonthlyInvoices: tenant {TenantId} punya PlanId {PlanId} yang tak ditemukan - dilewati.", t.Id, t.PlanId);
                continue;
            }

            var invoice = new Invoice { Id = Guid.NewGuid(), TenantId = t.Id, PeriodMonth = periodMonth, Amount = price, Status = InvoiceStatus.Issued };
            db.Invoices.Add(invoice);
            db.OutboxMessages.Add(new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Type = "InvoiceIssued",
                PayloadJson = JsonSerializer.Serialize(new { InvoiceId = invoice.Id, TenantId = t.Id }),
            });
        }

        await db.SaveChangesAsync();
        logger.LogInformation("GenerateMonthlyInvoices: {Month} -> {Count} invoice baru diterbitkan.", periodMonth, toInvoice.Count);
    }
}
