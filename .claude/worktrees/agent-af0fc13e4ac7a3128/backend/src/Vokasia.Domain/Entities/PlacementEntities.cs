using Vokasia.Domain.Common;

namespace Vokasia.Domain.Entities;

/// <summary>
/// Penempatan siswa→DUDI→guru→mentor untuk satu periode. Pusat gravitasi domain:
/// hampir semua entitas lain (jurnal, visit, assessment, certificate) menggantung di sini.
/// </summary>
public class Placement : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid StudentId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid PeriodId { get; set; }
    public Guid TeacherId { get; set; }
    /// <summary>Null sampai mentor menerima magic link & akun tertaut (FR-AUTH-03).</summary>
    public Guid? MentorUserId { get; set; }
    public string? MentorEmail { get; set; }
    public Domain.Common.PlacementStatus Status { get; set; } = Domain.Common.PlacementStatus.Active;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
