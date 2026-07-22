using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vokasia.Domain.Events;
using Vokasia.Infrastructure.Messaging;
using Vokasia.Infrastructure.Persistence;
using Vokasia.Infrastructure.Scheduling;

namespace Vokasia.Worker.Consumers;

/// <summary>
/// VOK-H4-E1 §2 — hitung streak berjalan siswa (reset saat bolong hari kerja). Konsumen TERPISAH
/// dari JournalSubmittedConsumer (keduanya reaksi ke event yang SAMA, ticket sendiri memisahkan
/// jadi 2 kelas) - keduanya menyentuh baris StudentDailyStatus yang SAMA, aman lewat
/// StudentDailyStatusUpsert (lihat doc-comment di sana).
///
/// Logika: cari hari kerja TEPAT SEBELUM hari ini (BusinessCalendar, skip weekend+Holiday period
/// ybs). Kalau StudentDailyStatus hari itu ADA & Streak-nya >=1 (artinya hari itu jg terisi,
/// bagian dari streak berjalan) -> streak hari ini = streak hari itu + 1. Kalau TIDAK ADA atau
/// Streak==0 (hari kerja sebelumnya kosong/bolong, ATAU memang belum ada riwayat sama sekali) ->
/// streak hari ini mulai dari 1 (hari ini sendiri SUDAH terisi, itulah kenapa event ini terpicu).
/// </summary>
public class StreakCounterConsumer(VokasiaDbContext db, IdempotencyGuard guard, ILogger<StreakCounterConsumer> logger)
    : IConsumer<JournalSubmittedEvent>
{
    public const string Name = nameof(StreakCounterConsumer);

    public async Task Consume(ConsumeContext<JournalSubmittedEvent> context)
    {
        var ct = context.CancellationToken;
        var messageId = context.MessageId ?? Guid.Empty;

        if (!await guard.EnsureNotProcessedAsync(Name, messageId, ct))
        {
            logger.LogInformation("{Consumer}: pesan {MessageId} sudah diproses sebelumnya, dilewati.", Name, messageId);
            return;
        }

        var msg = context.Message;

        var slot = await db.JournalSlots.AsNoTracking().FirstOrDefaultAsync(s => s.Id == msg.SlotId, ct);
        var placement = await db.Placements.AsNoTracking().FirstOrDefaultAsync(p => p.Id == msg.PlacementId, ct);
        if (slot is null || placement is null)
        {
            logger.LogWarning(
                "{Consumer}: JournalSlot {SlotId} atau Placement {PlacementId} tak ditemukan (JournalId {JournalId}) - dilewati.",
                Name, msg.SlotId, msg.PlacementId, msg.JournalId);
            await db.SaveChangesAsync(ct);
            return;
        }

        var previousBusinessDay = await BusinessCalendar.PreviousBusinessDayAsync(db, placement.PeriodId, slot.Date, ct);
        var previousStatus = await db.StudentDailyStatuses.AsNoTracking()
            .FirstOrDefaultAsync(x => x.StudentId == placement.StudentId && x.PeriodId == placement.PeriodId && x.Date == previousBusinessDay, ct);

        var newStreak = previousStatus is { Streak: >= 1 } ? previousStatus.Streak + 1 : 1;

        await StudentDailyStatusUpsert.ApplyAsync(
            db, placement.TenantId, placement.StudentId, placement.PeriodId, slot.Date,
            status => status.Streak = newStreak,
            ct);

        logger.LogInformation(
            "{Consumer}: streak siswa {StudentId} tgl {Date} -> {Streak} (JournalId {JournalId}).",
            Name, placement.StudentId, slot.Date, newStreak, msg.JournalId);
    }
}
