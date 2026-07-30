using Vokasia.Domain.Entities;

namespace Vokasia.Domain.Common;

/// <summary>
/// VOK-H3-E3 §1: kerangka guard immutability nilai final — pola SAMA PERSIS dengan
/// <see cref="Entities.JournalEntry.EnsureMutable"/>, disiapkan sekarang krn AC ticket H3-E3 eksplisit
/// minta kerangka ditulis (bukan diam-diam ditunda tanpa jejak). Belum dipanggil di mana pun sampai
/// H5-E1 mengimplementasikan FinalizeAssessment/SubmitMentorScores/SubmitTeacherScores — Assessment
/// entity sendiri baru berisi kolom, belum ada endpoint mutasi skor sama sekali di scope H1–H3.
///
/// Diletakkan sbg static guard TERPISAH (bukan method instance di Assessment, spt JournalEntry) krn
/// mutasi nilai final biasanya menyentuh DUA entity sekaligus (Assessment header + AssessmentScore
/// baris per aspek) — satu titik panggil guard sebelum menyentuh salah satu cukup, tidak perlu
/// duplikasi method di kedua entity.
/// </summary>
public static class AssessmentImmutabilityGuard
{
    /// <summary>Dipanggil SEBELUM mutasi apa pun ke Assessment atau AssessmentScore terkait (H5-E1: SubmitMentorScores, SubmitTeacherScores, update rubrik retroaktif, dst).</summary>
    public static void EnsureMutable(Assessment assessment)
    {
        if (assessment.IsFinal)
            throw new DomainImmutableException("assessment-final-immutable", "Nilai yang sudah difinalisasi tidak bisa diubah.");
    }
}
