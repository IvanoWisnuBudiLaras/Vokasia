using Vokasia.Domain.Entities;

namespace Vokasia.Domain.Common;

/// <summary>
/// VOK-H5-E1 §3 — <c>ComputeWeightedScore</c>: fungsi MURNI (tanpa I/O/DbContext), test-first sesuai
/// DoD ticket. Menghitung Σ(nilai aspek × bobot)/100, dibulatkan 2 desimal
/// <see cref="MidpointRounding.AwayFromZero"/> (bukan default .NET <c>ToEven</c>/banker's-rounding —
/// nilai akademik siswa harus konsisten dgn ekspektasi umum "0,5 selalu naik", bukan kadang naik
/// kadang tidak tergantung digit sebelumnya).
///
/// "Pembagian sisi mentor/guru sesuai Kind" (AC ticket) DITEGAKKAN di lapisan pemanggil (H5-E1
/// endpoint SubmitMentorScores/SubmitTeacherScores menulis <see cref="AssessmentScore"/> hanya utk
/// aspek dgn <see cref="RubricAspectKind"/> yg sesuai sisinya) — fungsi ini SENDIRI tidak peduli
/// siapa yg mengisi, hanya peduli SEMUA aspek rubrik sudah py tepat satu skor sebelum dihitung
/// (validasi kelengkapan, bukan validasi kepemilikan sisi).
/// </summary>
public static class AssessmentScoring
{
    /// <summary>
    /// Dilempar (bukan diam-diam anggap 0) kalau ada aspek rubrik yang belum py skor sama sekali —
    /// lihat AC ticket: "aspek belum diisi → exception eksplisit".
    /// </summary>
    public sealed class IncompleteScoresException(IReadOnlyList<string> missingAspectNames)
        : InvalidOperationException($"Skor belum lengkap untuk aspek: {string.Join(", ", missingAspectNames)}.")
    {
        public IReadOnlyList<string> MissingAspectNames { get; } = missingAspectNames;
    }

    public static decimal ComputeWeightedScore(IReadOnlyList<RubricAspect> aspects, IReadOnlyDictionary<Guid, decimal> scoresByAspectId)
    {
        var missing = aspects.Where(a => !scoresByAspectId.ContainsKey(a.Id)).Select(a => a.Name).ToList();
        if (missing.Count > 0)
        {
            throw new IncompleteScoresException(missing);
        }

        var weightedSum = aspects.Sum(a => scoresByAspectId[a.Id] * a.Weight);
        return Math.Round(weightedSum / 100m, 2, MidpointRounding.AwayFromZero);
    }
}
