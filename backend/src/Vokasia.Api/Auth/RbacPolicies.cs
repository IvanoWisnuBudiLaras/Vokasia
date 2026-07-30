using Vokasia.Domain.Common;

namespace Vokasia.Api.Auth;

/// <summary>
/// Nama policy baku RBAC (PRD matrix 2.3), dideklarasikan di ticket VOK-H2-E3 §2 dan dipakai
/// H2-E1's endpoints per [ASSUMPTION] ticket H2-E1 ("pakai nama policy yang dideklarasikan di
/// VOK-H2-E3 bila H2-E3 belum siap"). Diimplementasikan di sini (bukan diam-diam ditunda) karena
/// endpoint H2-E1 butuh policy nyata untuk berjalan, bukan hanya nama string tanpa arti.
/// </summary>
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
            // TenantMember: staff sekolah dengan tenant_id GUID valid. Presence saja tidak cukup:
            // claim malformed membuat ambient null dan menonaktifkan filter tenant.
            .AddPolicy(TenantMember, p => p.RequireAssertion(HasValidTenantId))
            .AddPolicy(MentorOwnPlacement, p => p
                .RequireClaim("role", nameof(UserRole.IndustryMentor))
                .AddRequirements(new PlacementScopeRequirement()));

        services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, PlacementScopeHandler>();

        return services;
    }

    private static bool HasValidTenantId(
        Microsoft.AspNetCore.Authorization.AuthorizationHandlerContext context) =>
        Guid.TryParse(context.User.FindFirst("tenant_id")?.Value, out _);
}

/// <summary>Requirement kosong (marker) — logika nyata di PlacementScopeHandler (resource-based).</summary>
public class PlacementScopeRequirement : Microsoft.AspNetCore.Authorization.IAuthorizationRequirement
{
}

/// <summary>
/// AC VOK-H2-E3: mentor hanya boleh beraksi pada Placement miliknya (Placement.MentorUserId == sub),
/// BUKAN semua placement tenant (mentor lintas-tenant secara desain, TenantId=null). Dipakai via
/// resource-based authorization: <c>await authService.AuthorizeAsync(User, placement, RbacPolicies.MentorOwnPlacement)</c>
/// di endpoint yang menerima instance Placement sebagai resource (mulai dipakai H3 ApproveJournal dst).
/// </summary>
public class PlacementScopeHandler : Microsoft.AspNetCore.Authorization.AuthorizationHandler<PlacementScopeRequirement, Vokasia.Domain.Entities.Placement>
{
    protected override Task HandleRequirementAsync(
        Microsoft.AspNetCore.Authorization.AuthorizationHandlerContext context,
        PlacementScopeRequirement requirement,
        Vokasia.Domain.Entities.Placement resource)
    {
        var sub = context.User.FindFirst(OpenIddict.Abstractions.OpenIddictConstants.Claims.Subject)?.Value;
        if (Guid.TryParse(sub, out var userId) && resource.MentorUserId == userId)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
