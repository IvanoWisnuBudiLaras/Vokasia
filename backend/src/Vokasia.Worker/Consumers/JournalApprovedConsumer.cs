using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vokasia.Domain.Common;
using Vokasia.Domain.Events;
using Vokasia.Infrastructure.Messaging;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Worker.Consumers;

/// <summary>
/// VOK-H4-E1 §2 — CreateNotification(siswa, JournalApproved).
///
/// [GAP dicatat, bukan diam-diam diimplementasi asal-asalan] Ticket menulis "+ proyeksi entri ke
/// bahan portofolio (kompetensi terverifikasi++)" - dikonfirmasi baca skema nyata
/// (CertificateAndPortfolioEntities.cs): <c>Portfolio</c> adalah profil publik OPT-IN KURASI SISWA
/// SENDIRI (Slug/Headline/SampleJournalIdsCsv - siswa PILIH sample jurnal mana yang mau
/// ditampilkan), BUKAN counter otomatis yang bertambah tiap approval. Tak ada field/tabel
/// "kompetensi terverifikasi" apa pun di skema saat ini - menambahnya spekulatif tanpa tahu bentuk
/// akhir fitur portofolio (kemungkinan besar ticket H6 tersendiri). Bagian KONKRET+TERUJI (AC
/// nyata: notifikasi siswa) dikerjakan penuh; bagian portofolio SENGAJA tidak dikarang skemanya
/// sesi ini - dicatat DECISIONS.md sbg gap eksplisit utk H6.
/// </summary>
public class JournalApprovedConsumer(VokasiaDbContext db, IdempotencyGuard guard, INotifier notifier, ILogger<JournalApprovedConsumer> logger)
    : IConsumer<JournalApprovedEvent>
{
    public const string Name = nameof(JournalApprovedConsumer);

    public async Task Consume(ConsumeContext<JournalApprovedEvent> context)
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
            notifier.CreateNotification(studentUserId.Value, NotificationType.JournalApproved, new { JournalId = msg.JournalId });
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("{Consumer}: jurnal {JournalId} disetujui, notifikasi terkirim.", Name, msg.JournalId);
    }
}
