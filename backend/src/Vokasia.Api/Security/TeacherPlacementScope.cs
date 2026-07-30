using Vokasia.Domain.Common;

namespace Vokasia.Api.Security;

public static class TeacherPlacementScope
{
    public static bool CanAccess(UserRole role, Guid callerUserId, Guid placementTeacherId) =>
        role is UserRole.TenantAdmin or UserRole.DeptHead || callerUserId == placementTeacherId;
}
