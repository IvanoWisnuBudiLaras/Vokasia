using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Messaging;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Worker.Jobs;

/// <summary>
/// VOK-H5-E1 §3 — OpenAssessmentPhase, cron harian 06:00 WIB (didaftar Program.cs, pola SAMA
/// persis dgn JournalCronJobs: DbContext tanpa AmbientTenantContext -> lintas semua tenant by
/// design). Periode `EndDate - 14 hari == hari ini` -> `Status=Assessment` + notif mentor & guru.
///
/// Idempoten BY CONSTRUCTION (bukan tanda "sudah pernah jalan" terpisah): query HANYA periode
/// `Status==Active` — begitu sebuah periode berpindah ke `Assessment` di run pertama, run
/// berikutnya (hari yang sama atau re-trigger manual) tidak akan menemukannya lagi di query WHERE
/// (filter `Status==Active` sudah tak cocok), notifikasi TIDAK dobel terkirim.
/// </summary>
public class AssessmentCronJobs(VokasiaDbContext db, INotifier notifier, ILogger<AssessmentCronJobs> logger)
{
    private static DateOnly TodayJakarta() => AppTimeZone.TodayJakarta();

    public async Task OpenAssessmentPhase(DateOnly? runDate = null)
    {
        var today = runDate ?? TodayJakarta();

        var periods = await db.Periods
            .Where(p => p.Status == PeriodStatus.Active && p.EndDate == today.AddDays(14))
            .ToListAsync();

        if (periods.Count == 0)
        {
            logger.LogInformation("OpenAssessmentPhase: {Date} tak ada periode yang H-14 dari EndDate.", today);
            return;
        }

        var notifiedPlacements = 0;
        foreach (var period in periods)
        {
            period.Status = PeriodStatus.Assessment;

            var placements = await db.Placements.AsNoTracking()
                .Where(p => p.PeriodId == period.Id && p.Status == PlacementStatus.Active)
                .ToListAsync();

            foreach (var placement in placements)
            {
                if (placement.MentorUserId.HasValue)
                {
                    notifier.CreateNotification(placement.MentorUserId.Value, NotificationType.AssessmentPhaseOpened, new { placement.Id, period.Name });
                }
                // Guru = AppUser langsung (Placement.TeacherId == AppUser.Id, pola sama dgn FlagGhostingStudents).
                notifier.CreateNotification(placement.TeacherId, NotificationType.AssessmentPhaseOpened, new { placement.Id, period.Name });
                notifiedPlacements++;
            }
        }

        await db.SaveChangesAsync();
        logger.LogInformation(
            "OpenAssessmentPhase: {Date} -> {PeriodCount} periode dibuka ke fase Assessment, {PlacementCount} placement dinotifikasi (mentor+guru).",
            today, periods.Count, notifiedPlacements);
    }

    /// <summary>
    /// VOK-H5-E1 §5 — cron 06:30 WIB (didaftar Program.cs, SETELAH OpenAssessmentPhase 06:00).
    /// "Periode finalized H+1" [ASSUMPTION, dicatat eksplisit - ticket tak beri definisi presisi]:
    /// Assessment yang di-FinalizeAssessment TEPAT KEMARIN (FinalizedAt.Date == today-1, zona WIB)
    /// - beri jeda 1 hari sblm sertifikat dibuat (kesempatan koreksi cepat kalau ada salah input
    /// sebelum PDF resmi tercetak). "Placement lulus" = SEMUA assessment yang berhasil difinalisasi
    /// (tak ada ambang nilai kelulusan terpisah di skema/AC manapun sampai ticket ini - finalisasi
    /// ITU SENDIRI yang jadi penanda "lulus penilaian PKL", bukan skor tertentu).
    ///
    /// Idempoten: skip placement yang SUDAH punya baris Certificate (bukan re-cek tanggal - re-run
    /// manual hari lain tak bikin dobel selama Certificate sudah ada).
    /// </summary>
    public async Task EnqueueCertificateBatch(DateOnly? runDate = null)
    {
        var today = runDate ?? TodayJakarta();
        var yesterday = today.AddDays(-1);

        var finalizedYesterday = await db.Assessments.AsNoTracking()
            .Where(a => a.IsFinal && a.FinalizedAt.HasValue)
            .Select(a => new { a.PlacementId, a.TenantId, a.FinalizedAt })
            .ToListAsync();

        var eligiblePlacementIds = finalizedYesterday
            .Where(a => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(a.FinalizedAt!.Value, AppTimeZone.Jakarta).Date) == yesterday)
            .Select(a => new { a.PlacementId, a.TenantId })
            .Distinct()
            .ToList();

        if (eligiblePlacementIds.Count == 0)
        {
            logger.LogInformation("EnqueueCertificateBatch: {Date} tak ada assessment finalized kemarin ({Yesterday}).", today, yesterday);
            return;
        }

        var placementIds = eligiblePlacementIds.Select(x => x.PlacementId).ToList();
        var alreadyHaveCertificate = (await db.Certificates.AsNoTracking()
            .Where(c => placementIds.Contains(c.PlacementId))
            .Select(c => c.PlacementId)
            .ToListAsync())
            .ToHashSet();

        var toEnqueue = eligiblePlacementIds.Where(x => !alreadyHaveCertificate.Contains(x.PlacementId)).ToList();
        if (toEnqueue.Count == 0)
        {
            logger.LogInformation("EnqueueCertificateBatch: {Date} semua placement finalized kemarin sudah punya sertifikat (idempoten, nol baru).", today);
            return;
        }

        foreach (var x in toEnqueue)
        {
            db.OutboxMessages.Add(new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Type = "CertificateRequested",
                PayloadJson = JsonSerializer.Serialize(new { PlacementId = x.PlacementId, TenantId = x.TenantId }),
            });
        }

        await db.SaveChangesAsync();
        logger.LogInformation("EnqueueCertificateBatch: {Date} -> {Count} CertificateRequested diantre (periode finalized {Yesterday}).", today, toEnqueue.Count, yesterday);
    }
}
