using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vokasia.Domain.Common;
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
}
