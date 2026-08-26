namespace Vokasia.Domain.Common;

/// <summary>
/// Zona waktu tunggal seluruh domain (WIB) — VOK-H3-E1. Diletakkan di Domain (bukan Worker/Api)
/// krn dipakai DUA arah: cron Worker (JournalCronJobs, jadwal 05:00/19:00 WIB) DAN endpoint Api
/// (GetTodayJournal, perlu resolusi "hari ini" yang KONSISTEN dgn tanggal yang dipakai cron saat
/// membuat slot) — Api tidak boleh referensi Worker (arah dependency salah, lihat .csproj masing2),
/// jadi konstanta bersama ini tinggal di Domain yang keduanya sudah referensi.
/// </summary>
public static class AppTimeZone
{
    public static readonly TimeZoneInfo Jakarta = TimeZoneInfo.FindSystemTimeZoneById("Asia/Jakarta");

    public static DateOnly TodayJakarta() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Jakarta));

    public static TimeSpan CalculateDevRefreshTokenLifetime(DateTimeOffset nowUtc)
    {
        var nowWib = TimeZoneInfo.ConvertTimeFromUtc(nowUtc.UtcDateTime, Jakarta);
        int daysToAdd = nowWib.Hour >= 12 ? 2 : 1;
        var targetMidnightWib = nowWib.Date.AddDays(daysToAdd);
        var targetMidnightUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(targetMidnightWib, Jakarta), TimeSpan.Zero);
        var lifetime = targetMidnightUtc - nowUtc;
        return lifetime.TotalSeconds > 0 ? lifetime : TimeSpan.FromHours(1);
    }
}
