namespace Vokasia.Domain.Entities;

/// <summary>Paket langganan GLOBAL (bukan tenant-scoped) — FR-SA-03.</summary>
public class Plan
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public decimal PriceMonthly { get; set; }
    public int MaxStudents { get; set; }
    public int MaxPlacements { get; set; }
}

/// <summary>
/// Feature flag per plan ATAU override per tenant (FR-SA-03). Salah satu dari PlanId/TenantId diisi.
/// Resolusi efektif: GetEffectiveFlags (H6-E1) — override tenant menang atas plan.
/// </summary>
public class FeatureFlag
{
    public Guid Id { get; set; }
    public Guid? PlanId { get; set; }
    public Guid? TenantId { get; set; }
    public string Key { get; set; } = default!;
    public bool Enabled { get; set; }
}

/// <summary>Tagihan bulanan tenant (FR-BIL-01/02).</summary>
public class Invoice
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public DateOnly PeriodMonth { get; set; } // tanggal 1 bulan terkait
    public decimal Amount { get; set; }
    public Common.InvoiceStatus Status { get; set; } = Common.InvoiceStatus.Issued;
    public string? ProofKey { get; set; }
}
