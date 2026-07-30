using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vokasia.Domain.Common;
using Vokasia.Domain.Events;
using Vokasia.Infrastructure.Messaging;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Worker.Consumers;

/// <summary>VOK-H4-E1 §2 — CreateNotification(siswa, JournalRejected) + alasan (payload sudah bawa
/// Reason langsung dari RejectJournal/JournalEndpoints.cs, tak perlu query tambahan).</summary>
public class JournalRejectedConsumer(VokasiaDbContext db, IdempotencyGuard guard, INotifier notifier, ILogger<JournalRejectedConsumer> logger)
    : IConsumer<JournalRejectedEvent>
{
    public const string Name = nameof(JournalRejectedConsumer);

    public async Task Consume(ConsumeContext<JournalRejectedEvent> context)
    {
        var ct = context.CancellationToken;
        var messageId = context.MessageId ?? Guid.Empty;

        if (!await guard.EnsureNotProcessedAsync(Name, messageId, ct))
        {
            logger.LogInformation("{Consumer}: pesan {MessageId} sudah diproses sebelumnya, dilewati.", Name, messageId);
            return;
        }

        var msg = context.Message;

        var studentUserId = await db.Placements.AsNoTracking().Where(p => p.Id == msg.PlacementId)
            .Join(db.Students.AsNoTracking(), p => p.StudentId, s => s.Id, (p, s) => s.UserId)
            .FirstOrDefaultAsync(ct);

        if (studentUserId is null)
        {
            logger.LogWarning(
                "{Consumer}: Placement {PlacementId} tak ditemukan atau siswa tanpa akun login (JournalId {JournalId}) - notifikasi dilewati.",
                Name, msg.PlacementId, msg.JournalId);
        }
        else
        {
            notifier.CreateNotification(studentUserId.Value, NotificationType.JournalRejected, new { JournalId = msg.JournalId, msg.Reason });
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("{Consumer}: jurnal {JournalId} ditolak, notifikasi terkirim.", Name, msg.JournalId);
    }
}
