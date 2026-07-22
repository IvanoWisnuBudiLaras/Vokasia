using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vokasia.Domain.Events;
using Vokasia.Infrastructure.Messaging;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Worker.Consumers;

/// <summary>
/// VOK-H4-E1 §2 — render template undangan mentor + kirim via IEmailSender (dev: log). Payload
/// asli (MagicLinkService.CreateInviteAsync) hanya bawa PlacementId, Email, ExpiresAt - StudentName
/// & CompanyName di-lookup di sini via join.
///
/// [CATATAN]: MagicLinkService SENDIRI sudah log magic link URL dev-only (H2-E3) - consumer INI
/// TIDAK punya token mentah (hanya TokenHash tersimpan DB, prinsip keamanan yang sama dgn refresh
/// token - lihat MentorInvite.cs) sehingga TIDAK BISA menyertakan link klik-langsung di emailnya
/// sendiri. Email di sini murni pemberitahuan "undangan sudah dibuat, cek WhatsApp/kontak lain dari
/// staf sekolah" - bukan pengganti mekanisme pengiriman link yang sudah ada.
/// </summary>
public class MentorInvitedConsumer(VokasiaDbContext db, IdempotencyGuard guard, IEmailSender emailSender, ILogger<MentorInvitedConsumer> logger)
    : IConsumer<MentorInvitedEvent>
{
    public const string Name = nameof(MentorInvitedConsumer);

    public async Task Consume(ConsumeContext<MentorInvitedEvent> context)
    {
        var ct = context.CancellationToken;
        var messageId = context.MessageId ?? Guid.Empty;

        if (!await guard.EnsureNotProcessedAsync(Name, messageId, ct))
        {
            logger.LogInformation("{Consumer}: pesan {MessageId} sudah diproses sebelumnya, dilewati.", Name, messageId);
            return;
        }

        var msg = context.Message;

        var info = await db.Placements.AsNoTracking().Where(p => p.Id == msg.PlacementId)
            .Join(db.Students.AsNoTracking(), p => p.StudentId, s => s.Id, (p, s) => new { p.CompanyId, StudentName = s.FullName })
            .Join(db.Companies.AsNoTracking(), x => x.CompanyId, c => c.Id, (x, c) => new { x.StudentName, CompanyName = c.Name })
            .FirstOrDefaultAsync(ct);

        if (info is null)
        {
            logger.LogWarning("{Consumer}: Placement {PlacementId} tak ditemukan - email undangan dilewati.", Name, msg.PlacementId);
            await db.SaveChangesAsync(ct);
            return;
        }

        var subject = $"Undangan mentor PKL - {info.StudentName}";
        var body = $"Anda diundang menjadi mentor pendamping PKL untuk {info.StudentName} di {info.CompanyName}. " +
                   $"Silakan hubungi staf sekolah untuk tautan aktivasi akun mentor (berlaku sampai {msg.ExpiresAt:d MMM yyyy}).";

        await emailSender.SendAsync(msg.Email, subject, body, ct);

        await db.SaveChangesAsync(ct); // simpan penanda idempotency guard.
        logger.LogInformation("{Consumer}: email undangan mentor {Email} terkirim (InviteId {InviteId}).", Name, msg.Email, msg.InviteId);
    }
}
