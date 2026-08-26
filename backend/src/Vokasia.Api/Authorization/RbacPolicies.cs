using Microsoft.AspNetCore.Authorization;
using Vokasia.Domain.Common;

namespace Vokasia.Api.Authorization;

public static class RbacPolicies
{
    public const string SaOnly = "SaOnly";
    public const string TenantAdminOnly = "TenantAdmin";
    public const string DeptHeadPlus = "DeptHead+";
    public const string TeacherPlus = "Teacher+";
    public const string MentorOwnPlacement = "MentorOwnPlacement";
    public const string StudentSelf = "StudentSelf";
    public const string TenantMember = "TenantMember";

    public static IServiceCollection AddVokasiaRbacPolicies(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(SaOnly, p => p.RequireClaim("role", nameof(UserRole.SuperAdmin)))
            .AddPolicy(TenantAdminOnly, p => p
                .RequireClaim("role", nameof(UserRole.TenantAdmin))
                .RequireAssertion(HasValidTenantId))
            .AddPolicy(DeptHeadPlus, p => p
                .RequireClaim("role", nameof(UserRole.TenantAdmin), nameof(UserRole.DeptHead))
                .RequireAssertion(HasValidTenantId))
            .AddPolicy(TeacherPlus, p => p
                .RequireClaim("role", nameof(UserRole.TenantAdmin), nameof(UserRole.DeptHead), nameof(UserRole.Teacher))
                .RequireAssertion(HasValidTenantId))
            .AddPolicy(StudentSelf, p => p
                .RequireClaim("role", nameof(UserRole.Student))
                .RequireAssertion(HasValidTenantId))
            .AddPolicy(TenantMember, p => p
                .RequireClaim("role", nameof(UserRole.TenantAdmin), nameof(UserRole.DeptHead), nameof(UserRole.Teacher))
                .RequireAssertion(HasValidTenantId))
            .AddPolicy(MentorOwnPlacement, p => p
                .RequireClaim("role", nameof(UserRole.IndustryMentor))
                .AddRequirements(new PlacementScopeRequirement()));

        services.AddScoped<IAuthorizationHandler, PlacementScopeHandler>();
        services.AddScoped<IAuthorizationHandler, CanManageTemplateHandler>();
        services.AddScoped<IAuthorizationHandler, CanReadSnapshotHandler>();
        services.AddScoped<IAuthorizationHandler, TeacherPlacementScopeHandler>();

        return services;
    }

    private static bool HasValidTenantId(AuthorizationHandlerContext context) =>
        Guid.TryParse(context.User.FindFirst("tenant_id")?.Value, out _);
}
