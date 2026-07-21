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
}
