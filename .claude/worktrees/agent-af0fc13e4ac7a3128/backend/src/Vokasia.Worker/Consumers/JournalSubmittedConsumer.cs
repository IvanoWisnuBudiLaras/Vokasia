using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vokasia.Domain.Common;
using Vokasia.Domain.Events;
using Vokasia.Infrastructure.Messaging;
using Vokasia.Infrastructure.Persistence;
using Vokasia.Infrastructure.Scheduling;

namespace Vokasia.Worker.Consumers;

/// <summary>
/// VOK-H4-E1 §2 — proyeksikan StudentDailyStatus (hari itu -> Rag=Green) supaya dashboard W3 baca
/// cepat tanpa hitung ulang tiap request (AC: "1 query utama, p95 &lt;300ms @900 siswa").
/// StudentId/PeriodId/Date TIDAK ada langsung di payload (lihat OutboxEventContracts.cs) -
/// di-lookup di sini via join SlotId->JournalSlot & PlacementId->Placement.
/// </summary>
public class JournalSubmittedConsumer(VokasiaDbContext db, IdempotencyGuard guard, ILogger<JournalSubmittedConsumer> logger)
    : IConsumer<JournalSubmittedEvent>
{
    public const string Name = nameof(JournalSubmittedConsumer);

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
            // Data sudah dihapus/tak konsisten sejak event ditulis - tak ada yang bisa diproyeksikan,
            // tapi INI BUKAN alasan utk retry (data tetap takkan muncul) - anggap selesai (idempotency
            // marker tetap disimpan di bawah), log warning supaya kelihatan kalau sering terjadi.
            logger.LogWarning(
                "{Consumer}: JournalSlot {SlotId} atau Placement {PlacementId} tak ditemukan (JournalId {JournalId}) - dilewati.",
                Name, msg.SlotId, msg.PlacementId, msg.JournalId);
            await db.SaveChangesAsync(ct); // simpan penanda idempotency guard walau tak ada proyeksi.
            return;
        }

        await StudentDailyStatusUpsert.ApplyAsync(
            db, placement.TenantId, placement.StudentId, placement.PeriodId, slot.Date,
            status => status.Rag = RagStatus.Green,
            ct);

        logger.LogInformation(
            "{Consumer}: StudentDailyStatus siswa {StudentId} tgl {Date} -> Green (JournalId {JournalId}, Resubmit={Resubmit}).",
            Name, placement.StudentId, slot.Date, msg.JournalId, msg.Resubmit);
    }
}
