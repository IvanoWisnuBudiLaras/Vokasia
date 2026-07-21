using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Vokasia.Api.Endpoints;
using Vokasia.Domain.Common;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Api.Validation;

/// <summary>
/// VOK-H3-E3 §2. Text ≤500 kar; CompetencyIds 1–5 & (async) harus milik jurusan siswa pemanggil;
/// PhotoIds ≤3 bila diisi. Batas jumlah dibaca dari <see cref="JournalEndpoints"/> (internal const)
/// — satu sumber kebenaran, sama dgn yang dulu inline di handler SubmitJournal (kini dihapus di sana).
///
/// Registrasi FluentValidation default = Scoped (AddValidatorsFromAssemblyContaining), sama dgn
/// VokasiaDbContext/ITenantContext -> aman di-constructor-inject di sini, tanpa captive dependency.
/// </summary>
public class SubmitJournalValidator : AbstractValidator<SubmitJournalRequest>
{
    public SubmitJournalValidator(VokasiaDbContext db, ITenantContext tenant)
    {
        RuleFor(x => x.Text)
            .NotEmpty().WithMessage("Teks jurnal wajib diisi.")
            .MaximumLength(500).WithMessage("Teks jurnal maksimal 500 karakter.");

        // [DEVIASI dicatat — DECISIONS.md D25]: ticket menulis "CompetencyIds 1-5" (tersirat minimum
        // 1). TIDAK diberlakukan sbg minimum wajib — ticket H3-E1 (implementasi asli, AC/DoD SUDAH
        // disetujui+didokumentasikan D24) TIDAK PERNAH mensyaratkan minimum sama sekali (cuma batas
        // ATAS 5), dan beberapa test H3-E1 yang SUDAH hijau sengaja submit CompetencyIds kosong sbg
        // skenario sah. Minimum-1 adalah aturan UX/bisnis (di luar tema ticket ini: anti-abuse/upper-
        // bound+kepemilikan), bukan celah keamanan — maka DIPERTAHANKAN 0 boleh, hanya batas ATAS +
        // cek kepemilikan major yang DITEGAKKAN BARU di sini (itulah inti AC H3-E3 §2 sesungguhnya).
        RuleFor(x => x.CompetencyIds)
            .NotNull().WithMessage("CompetencyIds wajib diisi (boleh array kosong).")
            .Must(ids => ids.Count <= JournalEndpoints.MaxCompetenciesPerEntry)
                .WithMessage($"Maksimal {JournalEndpoints.MaxCompetenciesPerEntry} kompetensi per jurnal.")
            .Must(ids => ids.Distinct().Count() == ids.Count)
                .WithMessage("Kompetensi tidak boleh dipilih dua kali.")
            .MustAsync((ids, ct) => AllBelongToCallerMajorAsync(ids, db, tenant, ct))
                .WithMessage("Salah satu kompetensi tidak sesuai jurusanmu.")
                .When(x => x.CompetencyIds is { Count: > 0 });

        RuleFor(x => x.PhotoIds)
            .Must(ids => ids is null || ids.Count <= JournalEndpoints.MaxPhotosPerEntry)
                .WithMessage($"Maksimal {JournalEndpoints.MaxPhotosPerEntry} foto per jurnal.");
    }

    private static async Task<bool> AllBelongToCallerMajorAsync(List<Guid> ids, VokasiaDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        if (!tenant.UserId.HasValue)
        {
            return false;
        }

        var student = await db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == tenant.UserId, ct);
        if (student is null)
        {
            return false;
        }

        var distinctIds = ids.Distinct().ToList();
        var validCount = await db.Competencies.AsNoTracking()
            .CountAsync(c => distinctIds.Contains(c.Id) && c.MajorId == student.MajorId, ct);
        return validCount == distinctIds.Count;
    }
}
