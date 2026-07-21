using Vokasia.Domain.Common;

namespace Vokasia.Domain.Entities;

/// <summary>Periode PKL satu tenant (mis. "PKL Ganjil 2026") — FR-TEN-01.</summary>
public class Period : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = default!;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    /// <summary>Kelas yang mengikuti, mis. "XII" — disimpan comma-separated sederhana untuk MVP.</summary>
    public string ClassLevels { get; set; } = default!;
    public PeriodStatus Status { get; set; } = PeriodStatus.Draft;
}

/// <summary>Kalender libur per periode — dipakai cron GenerateDailyJournalSlots untuk skip hari libur (FR-JRN-01).</summary>
public class Holiday : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid PeriodId { get; set; }
    public DateOnly Date { get; set; }
    public string Label { get; set; } = default!;
}
