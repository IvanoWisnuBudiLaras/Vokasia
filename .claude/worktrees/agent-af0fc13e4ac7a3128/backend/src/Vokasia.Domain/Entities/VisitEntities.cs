using Vokasia.Domain.Common;

namespace Vokasia.Domain.Entities;

/// <summary>Kunjungan monitoring guru ke DUDI (FR-ASM-01, wireframe W4).</summary>
public class Visit : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid PlacementId { get; set; }
    public Guid TeacherId { get; set; }
    public DateOnly Date { get; set; }
    public string Notes { get; set; } = default!;
    public string? PhotoKey { get; set; }
    public string? SignatureKey { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
