namespace Vokasia.Domain.Entities;

/// <summary>
/// DUDI (dunia usaha/industri) — registry GLOBAL lintas tenant (nilai jual utama Vokasia).
/// TIDAK tenant-scoped: satu Company bisa dipakai banyak sekolah (FR-SA-02).
/// </summary>
public class Company
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string? Sector { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? ContactPerson { get; set; }
    public bool IsVerified { get; set; }
    public Guid? MergedIntoId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Riwayat merge dua Company duplikat (FR-SA-02) — snapshot data sumber sebelum digabung.</summary>
public class CompanyMergeHistory
{
    public Guid Id { get; set; }
    public Guid SourceCompanyId { get; set; }
    public Guid TargetCompanyId { get; set; }
    public string SourceSnapshotJson { get; set; } = "{}";
    public Guid MergedByUserId { get; set; }
    public DateTimeOffset MergedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Link tenant ke DUDI global + kuota slot per periode (FR-TEN-04).</summary>
public class TenantCompany
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public DateTimeOffset LinkedAt { get; set; } = DateTimeOffset.UtcNow;

    public Company? Company { get; set; }
}

/// <summary>Kuota slot siswa per DUDI per periode (FR-TEN-04).</summary>
public class CompanySlot
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid PeriodId { get; set; }
    public int Slots { get; set; }
}
