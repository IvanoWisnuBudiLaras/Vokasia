using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vokasia.Api.Auth;
using Vokasia.Api.Validation;
using Vokasia.Domain.Common;
using Vokasia.Infrastructure.Identity;
using Vokasia.Infrastructure.Persistence;
using Vokasia.Infrastructure.Security;

namespace Vokasia.Api.Endpoints;

/// <summary>
/// VOK-H2-E1 §4: kelola user staf tenant (Teacher/DeptHead/TenantAdmin). TenantAdminOnly — membuat
/// akun & mengubah role adalah aksi sensitif, sengaja lebih ketat dari endpoint roster siswa.
/// </summary>
public static class SchoolUsersEndpoints
{
    public static IEndpointRouteBuilder MapSchoolUsersEndpoints(this IEndpointRouteBuilder app)
    {
        // VOK-H3-E3 §2: ValidationFilter global (InviteUserValidator: email format + role whitelist).
        var group = app.MapGroup("/api/school-users").WithTags("SchoolUsers")
            .RequireAuthorization(RbacPolicies.TenantAdminOnly)
            .AddEndpointFilter<ValidationFilter>();

        group.MapPost("/", InviteSchoolUser);
        group.MapPut("/{userId:guid}/role", AssignRole);
        group.MapGet("/", ListSchoolUsers);
        group.MapPost("/{userId:guid}/deactivate", DeactivateUser);

        return app;
    }

    private static async Task<IResult> InviteSchoolUser(
        InviteUserRequest req, UserManager<AppUser> userManager, ITenantContext tenant, CancellationToken ct)
    {
        if (!tenant.TenantId.HasValue)
        {
            return Results.Forbid();
        }

        if (req.Role is not (UserRole.Teacher or UserRole.DeptHead or UserRole.TenantAdmin))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Role"] = ["Hanya Teacher, DeptHead, atau TenantAdmin yang bisa diundang lewat endpoint ini."],
            });
        }

        var user = new AppUser
        {
            UserName = req.Email,
            Email = req.Email,
            FullName = req.FullName,
            TenantId = tenant.TenantId.Value,
            Role = req.Role,
        };

        // Password sementara acak — user set password sendiri via link undangan (email nyata: H4-E3).
        var tempPassword = $"Tmp-{Guid.NewGuid():N}Aa1!";
        var result = await userManager.CreateAsync(user, tempPassword);
        if (!result.Succeeded)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Email"] = result.Errors.Select(e => e.Description).ToArray(),
            });
        }

        // TODO-H4E3: kirim email undangan set-password nyata (SendEmail infra belum ada di H2).
        return Results.Created($"/api/school-users/{user.Id}", ToDto(user));
    }

    private static async Task<IResult> AssignRole(Guid userId, [FromBody] UserRole role, UserManager<AppUser> userManager, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return Results.NotFound();
        }

        user.Role = role;
        await userManager.UpdateAsync(user);
        return Results.Ok(ToDto(user));
    }

    private static async Task<IResult> ListSchoolUsers(
        VokasiaDbContext db, ITenantContext tenant, CancellationToken ct,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (!tenant.TenantId.HasValue)
        {
            return Results.Forbid();
        }

        var query = db.Users.AsNoTracking().Where(u => u.TenantId == tenant.TenantId);
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(u => u.FullName).Skip((page - 1) * pageSize).Take(pageSize).Select(u => ToDto(u)).ToListAsync(ct);

        return Results.Ok(new Paged<SchoolUserDto>(items, page, pageSize, total));
    }

    private static async Task<IResult> DeactivateUser(
        Guid userId,
        UserManager<AppUser> userManager,
        IBffSessionRevoker sessionRevoker,
        ITenantContext tenant,
        CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return Results.NotFound();
        }

        // UserManager.FindByIdAsync bypasses the tenant-scoped roster query. Keep this guard
        // explicit so a TenantAdmin cannot deactivate another tenant's user or a SuperAdmin.
        if (!tenant.TenantId.HasValue || user.TenantId != tenant.TenantId)
        {
            return Results.NotFound();
        }

        user.IsActive = false;
        var update = await userManager.UpdateAsync(user);
        if (!update.Succeeded)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["User"] = update.Errors.Select(e => e.Description).ToArray(),
            });
        }

        await sessionRevoker.RevokeUserSessionsAsync(user.Id, ct);
        return Results.NoContent();
    }

    private static SchoolUserDto ToDto(AppUser u) => new(u.Id, u.Email ?? "", u.FullName, u.Role, u.IsActive);
}
