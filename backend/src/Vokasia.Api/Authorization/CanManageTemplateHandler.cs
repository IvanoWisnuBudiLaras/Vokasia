using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Api.Authorization;

public class CanManageTemplateHandler : AuthorizationHandler<CanManageTemplateRequirement, LearningRecordTemplate>
{
    private readonly ITenantContext _tenant;
    private readonly VokasiaDbContext _db;

    public CanManageTemplateHandler(ITenantContext tenant, VokasiaDbContext db)
    {
        _tenant = tenant;
        _db = db;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        CanManageTemplateRequirement requirement,
        LearningRecordTemplate resource)
    {
        if (_tenant.Role == nameof(UserRole.TenantAdmin) && _tenant.TenantId.HasValue && _tenant.UserId.HasValue)
        {
            if (_tenant.TenantId == resource.TenantId)
            {
                context.Succeed(requirement);
                return;
            }
        }

        if (_tenant.Role != nameof(UserRole.IndustryMentor) || !_tenant.UserId.HasValue)
        {
            return;
        }

        // Mentor authorization (must have placement matching template parameters)
        var hasValidPlacement = await _db.Placements.AnyAsync(
            p => p.TenantId == resource.TenantId && p.CompanyId == resource.CompanyId && p.MentorUserId == _tenant.UserId.Value);

        if (hasValidPlacement)
        {
            context.Succeed(requirement);
        }
    }
}
