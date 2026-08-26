using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Identity;
using Vokasia.Infrastructure.Identity;
using Vokasia.Domain.Events;
using Vokasia.Infrastructure.Email;
using Vokasia.Infrastructure.Messaging;
using Vokasia.Infrastructure.Persistence;
using Vokasia.Infrastructure.Security;
using Vokasia.Infrastructure.Configuration;

namespace Vokasia.Worker.Consumers;

/// <summary>
/// VOK-H6-E1 §1 — kirim setup-link invitation after CreateTenant wizard
/// menulis baris OutboxMessage{TenantAdminInvited} dalam transaksi yang sama dgn Tenant+RubricTemplate+
/// AppUser (lihat SaTenantsEndpoints.CreateTenant). Payload SUDAH bawa semua field (Email/FullName/
/// The raw token exists only in the transient email message and is never persisted.
/// </summary>
public class TenantAdminInvitedConsumer(VokasiaDbContext db, IdempotencyGuard guard, IEmailSender emailSender, UserManager<AppUser> userManager, IConfiguration configuration, ILogger<TenantAdminInvitedConsumer> logger)
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

        var invitation = StaffInvitationToken.Create(DateTimeOffset.UtcNow);
        var invitedUser = await userManager.FindByIdAsync(msg.UserId.ToString()) ?? throw new InvalidOperationException("Invited user missing.");
        await userManager.SetAuthenticationTokenAsync(invitedUser, StaffInvitationToken.LoginProvider, StaffInvitationToken.Name, StaffInvitationToken.StoredValue(invitation.Hash, invitation.ExpiresAt));
        var setupUrl = $"{PublicAppOrigin.Resolve(configuration)}/set-password?token={Uri.EscapeDataString(invitation.Raw)}";
        var subject = $"Atur kata sandi Vokasia untuk {schoolName}";
        var html = $"<p>Halo {msg.FullName},</p><p><a href=\"{setupUrl}\">Atur kata sandi</a> dalam 24 jam.</p>";
        var text = $"Halo {msg.FullName}, atur kata sandi Anda melalui tautan ini: {setupUrl}. Tautan berlaku 24 jam.";
        await emailSender.SendAsync(new EmailMessage(msg.Email, "StaffInvitation", subject, html, text, messageId), ct);

        await db.SaveChangesAsync(ct); // simpan penanda idempotency guard.
        logger.LogInformation("{Consumer}: email undangan TenantAdmin {Email} (tenant {TenantId}) terkirim.", Name, msg.Email, msg.TenantId);
    }
}
