namespace Vokasia.Domain.Common;

/// <summary>
/// VOK-H3-E3 §1. Dilempar guard domain (JournalEntry.EnsureMutable, AssessmentImmutabilityGuard, dst)
/// saat entity yang sudah final (Approved/IsFinal) dicoba dimutasi SIAPA PUN — siswa, mentor, guru,
/// bahkan TenantAdmin (FR-JRN-04, NFR-SEC-08). Ini pelanggaran aturan bisnis yang DIHARAPKAN bisa
/// terjadi (user coba edit jurnal lama), bukan bug — maka bukan 500. Dipetakan middleware Api
/// (lihat Program.cs UseExceptionHandler) ke 409 Conflict + body konsisten { code, message }, supaya
/// FE bisa bedakan dari error validasi (400) atau error server (500) tanpa parse teks bebas.
///
/// TANPA jalur unlock di sini maupun di caller manapun — unlock ber-audit (SuperAdmin membuka
/// kembali entry final dengan jejak audit) SENGAJA ditunda ke backlog fase 2 (NFR-SEC-08, dicatat
/// eksplisit di TICKETS.md catatan pengendalian). Jangan tambahkan endpoint/flag "force unlock" di
/// scope H1–H7 tanpa keputusan Dev + entry DECISIONS.md baru.
/// </summary>
public class DomainImmutableException : Exception
{
    /// <summary>Kode mesin-bisa-baca (mis. "journal-approved-immutable") — stabil, dipakai FE utk cabang logika; message = teks manusia.</summary>
    public string Code { get; }

    public DomainImmutableException(string code, string? message = null) : base(message ?? code)
    {
        Code = code;
    }
}
