using System.ComponentModel.DataAnnotations;
using Vokasia.Domain.Common;

namespace Vokasia.Domain.Entities;

/// <summary>Template rubrik penilaian (default sesuai Panduan PKL Kurikulum Merdeka) — FR-ASM-02.</summary>
public class RubricTemplate : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = default!;
    public bool IsDefault { get; set; }
    public Guid? CompanyId { get; set; }
    public int Version { get; set; } = 1;
    public bool IsActive { get; set; } = true;

    public List<RubricAspect> Aspects { get; set; } = new();
}

/// <summary>Aspek penilaian + bobot; Σ Weight per template harus = 100 (divalidasi H5-E1).</summary>
public class RubricAspect
{
    public Guid Id { get; set; }
    public Guid RubricTemplateId { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public RubricAspectKind Kind { get; set; }
    public int Weight { get; set; }
}

/// <summary>
/// Header penilaian satu placement. FinalScore & IsFinal diisi FinalizeAssessment (H5-E1);
/// setelah IsFinal=true, immutable (guard penuh di H5, pola sama JournalEntry).
/// </summary>
public class Assessment : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid PlacementId { get; set; }
    public Guid RubricTemplateId { get; set; }
    public decimal? FinalScore { get; set; }
    [ConcurrencyCheck]
    public bool IsFinal { get; set; }
    public DateTimeOffset? FinalizedAt { get; set; }
}

/// <summary>Skor per aspek, diisi dua sisi (mentor: industri, guru: sekolah) — FR-ASM-03.</summary>
public class AssessmentScore
{
    public Guid Id { get; set; }
    public Guid AssessmentId { get; set; }
    public Guid RubricAspectId { get; set; }
    public ScoredBy ScoredBy { get; set; }
    public Guid ScoredByUserId { get; set; }
    public decimal Value { get; set; } // 0..100
    public string? Comment { get; set; }
}
