using Microsoft.AspNetCore.Identity;
using Vokasia.Domain.Common;

namespace Vokasia.Infrastructure.Identity;

/// <summary>
/// User Identity + field domain digabung jadi satu kelas (keputusan pragmatis MVP — lihat
/// DECISIONS.md D13). TenantId null untuk SuperAdmin & IndustryMentor (mereka lintas tenant/global).
/// </summary>
public class AppUser : IdentityUser<Guid>
{
    public Guid? TenantId { get; set; }
    public string FullName { get; set; } = default!;
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class AppRole : IdentityRole<Guid>
{
}
