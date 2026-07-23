namespace Vokasia.Domain.Common;

/// <summary>
/// VOK-H6-E1 §5 (FR-BIL-03) — dilempar CheckQuotaOnPlacement ketika placement aktif tenant sudah
/// mencapai/melewati Plan.MaxPlacements. Pelanggaran aturan bisnis yang DIHARAPKAN bisa terjadi (paket
/// habis), bukan bug — dipetakan ke 402 Payment Required (bukan 500) via QuotaExceededExceptionHandler
/// (Program.cs UseExceptionHandler), pola SAMA PERSIS dgn DomainImmutableException (H3-E3 §1).
///
/// [GAP/ASSUMPTION dicatat eksplisit]: ticket literal minta "MaxPlacements (+override)" — TIDAK ADA
/// field numerik per-tenant override apa pun di skema beku gate M0 (FeatureFlag hanya {Key,Enabled}
/// bool, bukan angka) - menambah kolom baru = ubah skema beku tanpa change control (PRD 3.6). Quota
/// dicek MURNI thd Plan.MaxPlacements tenant tsb, tanpa override numerik apa pun.
/// </summary>
public class QuotaExceededException : Exception
{
    public QuotaExceededException(string message) : base(message)
    {
    }
}
