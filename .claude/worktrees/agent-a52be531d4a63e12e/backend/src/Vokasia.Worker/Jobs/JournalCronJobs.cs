using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Messaging;
using Vokasia.Infrastructure.Persistence;
using Vokasia.Infrastructure.Scheduling;

namespace Vokasia.Worker.Jobs;

/// <summary>
/// AC VOK-H3-E1 §1 — cron Hangfire jurnal. Timezone Asia/Jakarta EKSPLISIT (bukan andalkan
/// server-local/UTC): baik penjadwalan Hangfire (<see cref="JakartaTimeZone"/>, dipakai di
/// <c>RecurringJobOptions</c> saat registrasi di Program.cs) MAUPUN resolusi "hari ini" internal
/// job ini sendiri (saat `runDate` tak diinjeksi) pakai zona yang sama — konsisten dgn AC proyek
/// yang berpatokan WIB (05:00/19:00 WIB, bukan jam server container yg mungkin UTC).
///
/// TIDAK di-scope per tenant (job ini SENGAJA jalan LINTAS SEMUA TENANT) — <see cref="VokasiaDbContext"/>
/// di Worker didaftar tanpa <c>AmbientTenantContext</c> pernah di-set (tak ada HTTP request per
/// eksekusi job), jadi query filter tenant otomatis "mati" (`!TenantId.HasValue` selalu true) —
/// query di sini melihat semua tenant by design, sama prinsipnya dgn MagicLinkService.ExchangeAsync
/// (VOK-H2-E3): DbContext scope tanpa tenant ambient = query lintas-tenant yang disengaja, bukan
/// kebocoran.
/// </summary>
public class JournalCronJobs(VokasiaDbContext db, INotifier notifier, ILogger<JournalCronJobs> logger)
{
    /// <summary>Passthrough ke <see cref="AppTimeZone.Jakarta"/> (Domain) — dipertahankan sbg nama
    /// stabil di sini krn Program.cs & test yang sudah ada merujuknya lewat kelas ini.</summary>
    public static TimeZoneInfo JakartaTimeZone => AppTimeZone.Jakarta;

    private static DateOnly TodayJakarta() => AppTimeZone.TodayJakarta();

    /// <summary>
    /// 05:00 WIB (didaftar via RecurringJob.AddOrUpdate, Program.cs). Buat <see cref="JournalSlot"/>
    /// utk setiap placement AKTIF yang: (a) tanggalnya dalam rentang Period.StartDate..EndDate,
    /// (b) bukan Sabtu/Minggu, (c) bukan tanggal di tabel Holiday period tsb, (d) belum punya slot
    /// utk tanggal ini. Idempoten BY CONSTRUCTION (query slot yg sudah ada dulu, filter, insert
    /// sisanya) — bukan andalkan DB unique-constraint-lalu-catch-exception, krn 1 baris gagal di
    /// Postgres akan me-rollback SELURUH batch tanpa savepoint per-baris eksplisit.
    /// </summary>
    public async Task GenerateDailyJournalSlots(DateOnly? runDate = null)
    {
        var date = runDate ?? TodayJakarta();

        if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            logger.LogInformation("GenerateDailyJournalSlots: {Date} akhir pekan, dilewati.", date);
            return;
        }

        var eligible = await db.Placements
            .Where(p => p.Status == PlacementStatus.Active)
            .Join(db.Periods,
                p => p.PeriodId,
                per => per.Id,
                (p, per) => new { p.Id, p.TenantId, PeriodId = per.Id, per.StartDate, per.EndDate })
            .Where(x => x.StartDate <= date && date <= x.EndDate)
            .ToListAsync();

        if (eligible.Count == 0)
        {
            logger.LogInformation("GenerateDailyJournalSlots: {Date} tak ada placement aktif dlm rentang periode manapun.", date);
            return;
        }

        var periodIds = eligible.Select(x => x.PeriodId).Distinct().ToList();
        var holidayPeriodIds = (await db.Holidays
            .Where(h => periodIds.Contains(h.PeriodId) && h.Date == date)
            .Select(h => h.PeriodId)
            .ToListAsync())
            .ToHashSet();

        var afterHoliday = eligible.Where(x => !holidayPeriodIds.Contains(x.PeriodId)).ToList();
        if (afterHoliday.Count == 0)
        {
            logger.LogInformation("GenerateDailyJournalSlots: {Date} tanggal libur bagi semua periode kandidat.", date);
            return;
        }

        var placementIds = afterHoliday.Select(x => x.Id).ToList();
        var existingSlotPlacementIds = (await db.JournalSlots
            .Where(s => placementIds.Contains(s.PlacementId) && s.Date == date)
            .Select(s => s.PlacementId)
            .ToListAsync())
            .ToHashSet();

        var toCreate = afterHoliday
            .Where(x => !existingSlotPlacementIds.Contains(x.Id))
            .Select(x => new JournalSlot
            {
                Id = Guid.NewGuid(),
                TenantId = x.TenantId,
                PlacementId = x.Id,
                Date = date,
                Status = JournalSlotStatus.Empty,
            })
            .ToList();

        if (toCreate.Count == 0)
        {
            logger.LogInformation("GenerateDailyJournalSlots: {Date} semua slot sudah ada (idempoten, nol baris baru).", date);
            return;
        }

        db.JournalSlots.AddRange(toCreate);
        await db.SaveChangesAsync();
        logger.LogInformation("GenerateDailyJournalSlots: {Date} -> {Count} slot baru dibuat.", date, toCreate.Count);
    }

    /// <summary>
    /// 19:00 WIB. Siswa dengan slot HARI INI berstatus <see cref="JournalSlotStatus.Empty"/> ->
    /// <see cref="Notification"/> (Type="JournalReminder") + event outbox utk konsumen email H4-E1
    /// (belum ada consumer di sesi ini — payload cukup lengkap utk H4 tinggal render+kirim).
    /// Siswa TANPA akun tertaut (<c>Student.UserId == null</c>) dilewati — tak ada penerima
    /// notifikasi yang valid.
    /// </summary>
    public async Task RemindEmptyJournals()
    {
        var date = TodayJakarta();

        var targets = await db.JournalSlots
            .Where(s => s.Date == date && s.Status == JournalSlotStatus.Empty)
            .Join(db.Placements, s => s.PlacementId, p => p.Id, (s, p) => new { Slot = s, p.StudentId })
            .Join(db.Students, x => x.StudentId, st => st.Id, (x, st) => new { x.Slot, st.UserId, st.FullName })
            .Where(x => x.UserId != null)
            .ToListAsync();

        if (targets.Count == 0)
        {
            logger.LogInformation("RemindEmptyJournals: {Date} tak ada slot kosong dgn siswa bertaut akun.", date);
            return;
        }

        foreach (var x in targets)
        {
            // VOK-H4-E1: lewat INotifier (satu pintu), bukan db.Notifications.Add inline lagi.
            notifier.CreateNotification(x.UserId!.Value, NotificationType.JournalReminder, new { slotId = x.Slot.Id, date = date.ToString("yyyy-MM-dd") });
            db.OutboxMessages.Add(new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Type = "JournalReminderEmailRequested",
                PayloadJson = JsonSerializer.Serialize(new { userId = x.UserId!.Value, slotId = x.Slot.Id, studentName = x.FullName, date = date.ToString("yyyy-MM-dd") }),
            });
        }

        await db.SaveChangesAsync();
        logger.LogInformation("RemindEmptyJournals: {Date} -> {Count} reminder dibuat (Notification + outbox JournalReminderEmailRequested).", date, targets.Count);
    }

    /// <summary>
    /// VOK-H4-E1 §3 — 21:00 WIB. Per placement AKTIF: hitung hari kerja BERTURUT-TURUT tanpa entry
    /// (mundur dari hari ini). Krn GenerateDailyJournalSlots HANYA membuat JournalSlot utk hari
    /// kerja (weekend+Holiday period tak pernah dapat slot sama sekali - lihat method di atas),
    /// setiap baris JournalSlot SUDAH PASTI hari kerja - cukup hitung slot Status=Empty berturut2
    /// dari yang TERBARU mundur, TANPA perlu BusinessCalendar lookup terpisah di sini.
    ///
    /// >=3 hari -> Rag=Red + notif guru&TenantAdmin (GhostingAlert) + event outbox email. 1-2 hari
    /// -> Amber. 0 hari (slot hari ini Filled, atau belum ada slot sama sekali) -> Green. Idempoten
    /// PER HARI by construction: StudentDailyStatusUpsert hitung ULANG dari data slot yang ada
    /// (bukan increment/toggle) - dijalankan 2x hari yang sama menghasilkan nilai akhir yang SAMA.
    /// </summary>
    public async Task FlagGhostingStudents()
    {
        var today = TodayJakarta();
        var activePlacements = await db.Placements.Where(p => p.Status == PlacementStatus.Active).ToListAsync();
        var flaggedCount = 0;

        foreach (var placement in activePlacements)
        {
            var recentSlots = await db.JournalSlots
                .Where(s => s.PlacementId == placement.Id && s.Date <= today)
                .OrderByDescending(s => s.Date)
                .Take(10) // cukup utk hitung sampai ambang tertinggi (>=3) tanpa scan seluruh riwayat placement.
                .ToListAsync();

            var consecutiveEmpty = 0;
            foreach (var slot in recentSlots)
            {
                if (slot.Status != JournalSlotStatus.Empty)
                {
                    break;
                }
                consecutiveEmpty++;
            }

            var rag = consecutiveEmpty switch
            {
                0 => RagStatus.Green,
                1 or 2 => RagStatus.Amber,
                _ => RagStatus.Red,
            };

            await StudentDailyStatusUpsert.ApplyAsync(
                db, placement.TenantId, placement.StudentId, placement.PeriodId, today,
                status => status.Rag = rag, default);

            if (consecutiveEmpty < 3)
            {
                continue;
            }

            flaggedCount++;
            var studentName = await db.Students.AsNoTracking().Where(s => s.Id == placement.StudentId)
                .Select(s => s.FullName).FirstOrDefaultAsync() ?? "-";
            var alertPayload = new { StudentName = studentName, Days = consecutiveEmpty };

            // Guru = AppUser langsung (Placement.TeacherId == AppUser.Id).
            notifier.CreateNotification(placement.TeacherId, NotificationType.GhostingAlert, alertPayload);

            var tenantAdminIds = await db.Users.AsNoTracking()
                .Where(u => u.TenantId == placement.TenantId && u.Role == UserRole.TenantAdmin)
                .Select(u => u.Id)
                .ToListAsync();
            foreach (var adminId in tenantAdminIds)
            {
                notifier.CreateNotification(adminId, NotificationType.GhostingAlert, alertPayload);
            }

            db.OutboxMessages.Add(new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Type = "GhostingAlertEmailRequested",
                PayloadJson = JsonSerializer.Serialize(new { PlacementId = placement.Id, StudentName = studentName, Days = consecutiveEmpty }),
            });

            await db.SaveChangesAsync(); // simpan notif+outbox placement ini sebelum lanjut ke placement berikutnya.
        }

        logger.LogInformation("FlagGhostingStudents: {Date} -> {Flagged}/{Total} placement ditandai ghosting (>=3 hari kerja kosong berturut-turut).", today, flaggedCount, activePlacements.Count);
    }
}
