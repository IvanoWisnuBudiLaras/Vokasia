using Microsoft.AspNetCore.Authorization;
using OpenIddict.Abstractions;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;

namespace Vokasia.Api.Authorization;

public class TeacherPlacementScopeHandler : AuthorizationHandler<TeacherPlacementScopeRequirement, Placement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TeacherPlacementScopeRequirement requirement,
        Placement resource)
    {
        var roleValue = context.User.FindFirst("role")?.Value;
        if (Enum.TryParse<UserRole>(roleValue, ignoreCase: true, out var role) &&
            (role == UserRole.TenantAdmin || role == UserRole.DeptHead))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var sub = context.User.FindFirst(OpenIddictConstants.Claims.Subject)?.Value;
        if (Guid.TryParse(sub, out var userId) && resource.TeacherId == userId)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}