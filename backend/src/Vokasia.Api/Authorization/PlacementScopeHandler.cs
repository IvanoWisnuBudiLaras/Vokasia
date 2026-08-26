using Microsoft.AspNetCore.Authorization;
using OpenIddict.Abstractions;
using Vokasia.Domain.Entities;

namespace Vokasia.Api.Authorization;

public class PlacementScopeRequirement : IAuthorizationRequirement
{
}

public class PlacementScopeHandler : AuthorizationHandler<PlacementScopeRequirement, Placement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PlacementScopeRequirement requirement,
        Placement resource)
    {
        var sub = context.User.FindFirst(OpenIddictConstants.Claims.Subject)?.Value;
        if (Guid.TryParse(sub, out var userId) && resource.MentorUserId == userId)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
