using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vokasia.Domain.Events;
using Vokasia.Infrastructure.Email;
using Vokasia.Infrastructure.Messaging;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Worker.Consumers;

/// <summary>
/// VOK-H6-E1 §5 — email InvoiceIssued ke TenantAdmin tenant terkait, menutup gap D30 "InvoiceIssued
/// dirender tapi belum dipanggil consumer produksi apa pun" (lihat doc-comment EmailTemplateRenderer)
/// - sekarang punya pemanggil nyata (BillingCronJobs.GenerateMonthlyInvoices).
/// </summary>
public class InvoiceIssuedConsumer(VokasiaDbContext db, IdempotencyGuard guard, IEmailSender emailSender, ILogger<InvoiceIssuedConsumer> logger)
    : IConsumer<InvoiceIssuedEvent>
{
    public const string Name = nameof(InvoiceIssuedConsumer);
    private const int DueDays = 14; // [ASSUMPTION] jatuh tempo H+14 dari terbit - tak ada field DueDate di skema Invoice (beku gate M0), tanggal ditampilkan email SAJA (bukan disimpan kolom baru).

    public async Task Consume(ConsumeContext<InvoiceIssuedEvent> context)
    {
        var ct = context.CancellationToken;
        var messageId = context.MessageId ?? Guid.Empty;

        if (!await guard.EnsureNotProcessedAsync(Name, messageId, ct))
        {
            logger.LogInformation("{Consumer}: pesan {MessageId} sudah diproses sebelumnya, dilewati.", Name, messageId);
            return;
        }

        var msg = context.Message;
        var invoice = await db.Invoices.AsNoTracking().FirstOrDefaultAsync(i => i.Id == msg.InvoiceId, ct);
        if (invoice is null)
        {
            logger.LogWarning("{Consumer}: Invoice {InvoiceId} tak ditemukan - email dilewati.", Name, msg.InvoiceId);
            await db.SaveChangesAsync(ct);
            return;
        }

        var tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == msg.TenantId, ct);
        var tenantAdmin = await db.Users.AsNoTracking()
            .Where(u => u.TenantId == msg.TenantId && u.Role == Domain.Common.UserRole.TenantAdmin && u.IsActive)
            .Select(u => new { u.Email, u.FullName })
            .FirstOrDefaultAsync(ct);

        if (tenant is null || tenantAdmin is null || string.IsNullOrWhiteSpace(tenantAdmin.Email))
        {
            logger.LogWarning("{Consumer}: tenant {TenantId} atau TenantAdmin aktifnya tak ditemukan - email dilewati.", Name, msg.TenantId);
            await db.SaveChangesAsync(ct);
            return;
        }

        var monthLabel = invoice.PeriodMonth.ToString("MMMM yyyy");
        var dueDate = invoice.PeriodMonth.AddDays(DueDays); // [ASSUMPTION] jatuh tempo tgl 15 (PeriodMonth = tgl 1 + 14 hari) - lihat catatan DueDays di atas.
        var (subject, html, text) = EmailTemplateRenderer.InvoiceIssued(tenant.SchoolName, monthLabel, invoice.Amount, dueDate);
        await emailSender.SendAsync(new EmailMessage(tenantAdmin.Email, "InvoiceIssued", subject, html, text, messageId), ct);

        await db.SaveChangesAsync(ct);
        logger.LogInformation("{Consumer}: email invoice {InvoiceId} (tenant {TenantId}) terkirim ke {Email}.", Name, invoice.Id, msg.TenantId, tenantAdmin.Email);
    }
}
