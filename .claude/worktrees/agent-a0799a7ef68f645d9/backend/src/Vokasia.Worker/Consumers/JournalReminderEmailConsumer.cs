using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vokasia.Domain.Events;
using Vokasia.Infrastructure.Email;
using Vokasia.Infrastructure.Messaging;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Worker.Consumers;

/// <summary>
/// VOK-H4-E3 §2 "wire ke consumer H4-E1: reminder... kirim email nyata (dev: DevLog)" — consumer
/// BARU utk <see cref="JournalReminderEmailRequestedEvent"/> (ditulis JournalCronJobs.RemindEmptyJournals
/// sejak H4-E1, TANPA consumer sampai ticket ini - lihat doc-comment event itu sendiri).
///
/// Payload py StudentName (snapshot at cron time) - TIDAK perlu join balik ke Students (beda dari
/// MentorInvitedConsumer yg payload aslinya TIDAK punya nama). Email tujuan: AppUser (UserId) - PERLU
/// join ke AspNetUsers.Email (Student.UserId != Notification.UserId secara TIPE - UserId di payload
/// ini SUDAH AppUser.Id, sama persis nilai yg dipakai Notifier.CreateNotification di cron asal).
/// </summary>
public class JournalReminderEmailConsumer(VokasiaDbContext db, IdempotencyGuard guard, IEmailSender emailSender, ILogger<JournalReminderEmailConsumer> logger)
    : IConsumer<JournalReminderEmailRequestedEvent>
{
    public const string Name = nameof(JournalReminderEmailConsumer);

    public async Task Consume(ConsumeContext<JournalReminderEmailRequestedEvent> context)
    {
        var ct = context.CancellationToken;
        var messageId = context.MessageId ?? Guid.Empty;

        if (!await guard.EnsureNotProcessedAsync(Name, messageId, ct))
        {
            logger.LogInformation("{Consumer}: pesan {MessageId} sudah diproses sebelumnya, dilewati.", Name, messageId);
            return;
        }

        var msg = context.Message;

        var email = await db.Users.AsNoTracking().Where(u => u.Id == msg.UserId).Select(u => u.Email).FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(email))
        {
            logger.LogWarning("{Consumer}: User {UserId} tak ditemukan/tanpa email - reminder dilewati.", Name, msg.UserId);
            await db.SaveChangesAsync(ct);
            return;
        }

        var date = DateOnly.Parse(msg.Date);
        var (subject, html, text) = EmailTemplateRenderer.JournalReminder(msg.StudentName, date);
        await emailSender.SendAsync(new EmailMessage(email, "JournalReminder", subject, html, text, messageId), ct);

        await db.SaveChangesAsync(ct);
        logger.LogInformation("{Consumer}: reminder jurnal {Email} diproses (slot {SlotId}).", Name, email, msg.SlotId);
    }
}
