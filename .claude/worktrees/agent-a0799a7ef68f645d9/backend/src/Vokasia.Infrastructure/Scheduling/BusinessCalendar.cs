using Microsoft.EntityFrameworkCore;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Infrastructure.Scheduling;

/// <summary>
/// VOK-H4-E1 — hari kerja PKL (Sen-Jum, minus tanggal di tabel Holiday milik Period tsb). Dipakai
/// StreakCounterConsumer (cari hari kerja SEBELUMNYA utk sambung/putus streak) & FlagGhostingStudents
/// cron (hitung berapa hari kerja BERTURUT-TURUT kosong) - SATU sumber kebenaran "apa itu hari
/// kerja" supaya kedua tempat tak diam-diam berbeda definisi (mis. lupa skip Holiday di satu
/// tempat tapi tidak di tempat lain).
/// </summary>
public static class BusinessCalendar
{
    public static async Task<bool> IsBusinessDayAsync(VokasiaDbContext db, Guid periodId, DateOnly date, CancellationToken ct)
    {
        if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            return false;
        }
        return !await db.Holidays.AsNoTracking().AnyAsync(h => h.PeriodId == periodId && h.Date == date, ct);
    }

    /// <summary>Hari kerja tepat SEBELUM `date` (mundur lewati weekend+Holiday). Loop dibatasi wajar
    /// (365 iterasi) - kalau period rusak/holiday tak masuk akal panjang, berhenti drpd infinite loop.</summary>
    public static async Task<DateOnly> PreviousBusinessDayAsync(VokasiaDbContext db, Guid periodId, DateOnly date, CancellationToken ct)
    {
        var candidate = date.AddDays(-1);
        for (var guard = 0; guard < 365; guard++)
        {
            if (await IsBusinessDayAsync(db, periodId, candidate, ct))
            {
                return candidate;
            }
            candidate = candidate.AddDays(-1);
        }
        return candidate; // fallback wajar - tak pernah realistis tercapai di data asli.
    }
}
