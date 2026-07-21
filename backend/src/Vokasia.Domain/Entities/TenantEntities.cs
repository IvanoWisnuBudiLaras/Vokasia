namespace Vokasia.Domain.Entities;

/// <summary>Satu sekolah SMK — unit isolasi multi-tenant (PRD §1.3, FR-SA-01).</summary>
public class Tenant
{
    public Guid Id { get; set; }
    public string SchoolName { get; set; } = default!;
    public string? Npsn { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public Guid? PlanId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Plan? Plan { get; set; }
}
