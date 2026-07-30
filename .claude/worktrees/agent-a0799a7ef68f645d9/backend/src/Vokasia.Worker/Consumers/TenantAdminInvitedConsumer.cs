using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vokasia.Domain.Events;
using Vokasia.Infrastructure.Email;
using Vokasia.Infrastructure.Messaging;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Worker.Consumers;

/// <summary>
/// VOK-H6-E1 §1 — kirim email TenantAdminInvite (password sementara) setelah CreateTenant wizard
/// menulis baris OutboxMessage{TenantAdminInvited} dalam transaksi yang sama dgn Tenant+RubricTemplate+
/// AppUser (lihat SaTenantsEndpoints.CreateTenant). Payload SUDAH bawa semua field (Email/FullName/
/// TempPassword) — TIDAK perlu look-up DB spt MentorInvitedConsumer (placement tak relevan di sini).
/// </summary>
public class TenantAdminInvitedConsumer(VokasiaDbContext db, IdempotencyGuard guard, IEmailSender emailSender, ILogger<TenantAdminInvitedConsumer> logger)
    : IConsumer<TenantAdminInvitedEvent>
{
    public const string Name = nameof(TenantAdminInvitedConsumer);

    public async Task Consume(ConsumeContext<TenantAdminInvitedEvent> context)
    {
        var ct = context.CancellationToken;
        var messageId = context.MessageId ?? Guid.Empty;

        if (!await guard.EnsureNotProcessedAsync(Name, messageId, ct))
        {
            logger.LogInformation("{Consumer}: pesan {MessageId} sudah diproses sebelumnya, dilewati.", Name, messageId);
            return;
        }

        var msg = context.Message;

        // SchoolName tidak dibawa payload (event = pointer tipis, pola sama file lain di
        // OutboxEventContracts.cs) — look-up singkat via Tenants (global, tanpa filter tenant di Worker).
        var schoolName = await db.Tenants.AsNoTracking()
            .Where(t => t.Id == msg.TenantId)
            .Select(t => t.SchoolName)
            .FirstOrDefaultAsync(ct) ?? "Sekolah Anda";

        var (subject, html, text) = EmailTemplateRenderer.TenantAdminInvite(schoolName, msg.FullName, msg.TempPassword);
        await emailSender.SendAsync(new EmailMessage(msg.Email, "TenantAdminInvite", subject, html, text, messageId), ct);

        await db.SaveChangesAsync(ct); // simpan penanda idempotency guard.
        logger.LogInformation("{Consumer}: email undangan TenantAdmin {Email} (tenant {TenantId}) terkirim.", Name, msg.Email, msg.TenantId);
    }
}
