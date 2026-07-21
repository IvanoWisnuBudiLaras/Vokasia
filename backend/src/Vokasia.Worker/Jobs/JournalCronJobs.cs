using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Persistence;

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
public class JournalCronJobs(VokasiaDbContext db, ILogger<JournalCronJobs> logger)
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
            db.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = x.UserId!.Value,
                Type = "JournalReminder",
                PayloadJson = JsonSerializer.Serialize(new { slotId = x.Slot.Id, date = date.ToString("yyyy-MM-dd") }),
            });
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
}
