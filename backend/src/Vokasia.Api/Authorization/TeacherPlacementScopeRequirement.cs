using Microsoft.AspNetCore.Authorization;

namespace Vokasia.Api.Authorization;

/// <summary>
/// Allows TenantAdmin and DeptHead globally; otherwise requires the caller to be the assigned teacher
/// of the Placement (resource).
/// </summary>
public class TeacherPlacementScopeRequirement : IAuthorizationRequirement
{
}