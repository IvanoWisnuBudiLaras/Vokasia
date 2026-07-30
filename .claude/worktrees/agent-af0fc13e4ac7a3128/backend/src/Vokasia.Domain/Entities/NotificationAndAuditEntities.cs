namespace Vokasia.Domain.Entities;

/// <summary>Notifikasi in-app (bell FE). Email dikirim terpisah oleh consumer H4-E3.</summary>
public class Notification
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Type { get; set; } = default!; // JournalApproved, GhostingAlert, dst.
    public string PayloadJson { get; set; } = "{}";
    public bool IsRead { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Jejak audit aksi sensitif (FR-X-01). ActingAsUserId diisi saat SuperAdmin impersonasi (H6-E3) —
/// bila null, ActorUserId bertindak atas namanya sendiri.
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }
    public Guid ActorUserId { get; set; }
    public Guid? ActingAsUserId { get; set; }
    public string Action { get; set; } = default!;
    public string Entity { get; set; } = default!;
    public string EntityId { get; set; } = default!;
    public string MetaJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
