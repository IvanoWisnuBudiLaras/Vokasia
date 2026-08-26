using Microsoft.AspNetCore.Authorization;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;

namespace Vokasia.Api.Authorization;

public class CanReadSnapshotHandler : AuthorizationHandler<CanReadSnapshotRequirement, Placement>
{
    private readonly ITenantContext _tenant;

    public CanReadSnapshotHandler(ITenantContext tenant)
    {
        _tenant = tenant;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        CanReadSnapshotRequirement requirement,
        Placement resource)
    {
        if (_tenant.Role == nameof(UserRole.TenantAdmin) && _tenant.TenantId.HasValue && _tenant.UserId.HasValue)
        {
            if (_tenant.TenantId == resource.TenantId)
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }
        }

        if (_tenant.Role == nameof(UserRole.IndustryMentor) && _tenant.UserId.HasValue)
        {
            if (resource.MentorUserId == _tenant.UserId.Value)
            {
                context.Succeed(requirement);
            }
        }

        return Task.CompletedTask;
    }
}
