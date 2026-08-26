using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vokasia.Api.Auth;
using Vokasia.Api.Authorization;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Identity;
using Vokasia.Infrastructure.Persistence;
using Vokasia.Infrastructure.Security;

namespace Vokasia.Api.Endpoints;

/// <summary>
/// Slice 5 SuperAdmin user operations. Cross-tenant reads and writes stay behind SaOnly;
/// tenant roles never reuse the SuperAdmin surface.
/// </summary>
public static class SaAdminEndpoints
{
    private static readonly UserRole[] ManagedRoles = [UserRole.TenantAdmin, UserRole.DeptHead, UserRole.Teacher];

    public static IEndpointRouteBuilder MapSaAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/sa").WithTags("SaAdmin").RequireAuthorization(RbacPolicies.SaOnly);
        group.MapGet("/tenants/{tenantId:guid}/users", ListTenantUsers);
        group.MapGet("/tenants/{tenantId:guid}/usage", GetTenantUsage);
        group.MapGet("/users/{userId:guid}", GetUser);
        group.MapPut("/users/{userId:guid}/role", ChangeRole);
        group.MapPost("/users/{userId:guid}/deactivate", DeactivateUser);
        group.MapPost("/users/{userId:guid}/reactivate", ReactivateUser);
        return app;
    }

    private static async Task<IResult> ListTenantUsers(
        Guid tenantId,
        VokasiaDbContext db,
        CancellationToken ct,
        [FromQuery] bool? active = true,
        [FromQuery] UserRole? role = null,
        [FromQuery] string? search = null)
    {
        if (!await db.Tenants.AsNoTracking().AnyAsync(t => t.Id == tenantId, ct))
        {
            return Results.NotFound();
        }

        var query = db.Users.AsNoTracking().Where(u => u.TenantId == tenantId && ManagedRoles.Contains(u.Role));
        if (active.HasValue) query = query.Where(u => u.IsActive == active.Value);
        if (role.HasValue) query = query.Where(u => u.Role == role.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(u => u.FullName.ToLower().Contains(term) || (u.Email != null && u.Email.ToLower().Contains(term)));
        }

        var tenantName = await db.Tenants.AsNoTracking().Where(t => t.Id == tenantId).Select(t => t.SchoolName).SingleAsync(ct);
        var users = await query.OrderBy(u => u.FullName)
            .Select(u => new SaUserDto(u.Id, tenantId, tenantName, u.Email ?? "", u.FullName, u.Role, u.IsActive, u.CreatedAt))
            .ToListAsync(ct);
        return Results.Ok(users);
    }

    private static async Task<IResult> GetTenantUsage(Guid tenantId, VokasiaDbContext db, CancellationToken ct)
    {
        if (!await db.Tenants.AsNoTracking().AnyAsync(t => t.Id == tenantId, ct))
        {
            return Results.NotFound();
        }

        var users = await db.Users.AsNoTracking()
            .Where(u => u.TenantId == tenantId && ManagedRoles.Contains(u.Role))
            .Select(u => new { u.IsActive, u.Role })
            .ToListAsync(ct);
        var activeMentors = await db.Placements.AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.Status == PlacementStatus.Active && p.MentorUserId.HasValue)
            .Select(p => p.MentorUserId!.Value)
            .Distinct()
            .CountAsync(ct);
        var activeStudents = await db.Placements.AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.Status == PlacementStatus.Active)
            .Select(p => p.StudentId)
            .Distinct()
            .CountAsync(ct);
        var activePlacements = await db.Placements.AsNoTracking()
            .CountAsync(p => p.TenantId == tenantId && p.Status == PlacementStatus.Active, ct);

        return Results.Ok(new SaTenantUsageDto(
            users.Count(u => u.IsActive),
            users.Count(u => !u.IsActive),
            activeStudents,
            activePlacements,
            activeMentors,
            users.Count(u => u.IsActive && u.Role == UserRole.Teacher)));
    }

    private static async Task<IResult> GetUser(Guid userId, VokasiaDbContext db, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId && ManagedRoles.Contains(u.Role), ct);
        if (user is null || !user.TenantId.HasValue)
        {
            return Results.NotFound();
        }

        var tenantName = await db.Tenants.AsNoTracking().Where(t => t.Id == user.TenantId.Value).Select(t => t.SchoolName).SingleOrDefaultAsync(ct) ?? "Tenant tidak ditemukan";
        var dto = ToDto(user, tenantName);
        var activity = await db.AuditLogs.AsNoTracking()
            .Where(a => a.ActorUserId == userId || a.ActingAsUserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .Take(10)
            .Select(a => new AuditDto(a.Id, a.TenantId, a.ActorUserId, a.ActingAsUserId, a.Action, a.Entity, a.EntityId, a.MetaJson, a.CreatedAt))
            .ToListAsync(ct);
        return Results.Ok(new SaUserDetailDto(dto, AccessFor(user.Role), activity));
    }

    private static async Task<IResult> ChangeRole(
        Guid userId,
        [FromBody] UserRole role,
        UserManager<AppUser> userManager,
        VokasiaDbContext db,
        ITenantContext actor,
        CancellationToken ct)
    {
        if (!ManagedRoles.Contains(role))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["Role"] = ["Role yang bisa dikelola: TenantAdmin, DeptHead, Teacher."] });
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && ManagedRoles.Contains(u.Role), ct);
        if (user is null || !user.TenantId.HasValue)
        {
            return Results.NotFound();
        }
        if (user.Role == role)
        {
            return Results.Conflict(new { message = "Role akun sudah sama." });
        }

        var previous = user.Role;
        user.Role = role;
        var update = await userManager.UpdateAsync(user);
        if (!update.Succeeded)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["User"] = update.Errors.Select(e => e.Description).ToArray() });
        }

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = user.TenantId,
            ActorUserId = actor.UserId ?? Guid.Empty,
            Action = "SuperAdminRoleChanged",
            Entity = nameof(AppUser),
            EntityId = user.Id.ToString(),
            MetaJson = JsonSerializer.Serialize(new { PreviousRole = previous, NewRole = role }),
        });
        await db.SaveChangesAsync(ct);
        var tenantName = await db.Tenants.AsNoTracking().Where(t => t.Id == user.TenantId.Value).Select(t => t.SchoolName).SingleAsync(ct);
        return Results.Ok(ToDto(user, tenantName));
    }

    private static async Task<IResult> DeactivateUser(
        Guid userId,
        SaUserActionRequest req,
        UserManager<AppUser> userManager,
        VokasiaDbContext db,
        ITenantContext actor,
        IBffSessionRevoker sessionRevoker,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Reason))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["Reason"] = ["Alasan wajib diisi."] });
        }
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && ManagedRoles.Contains(u.Role), ct);
        if (user is null || !user.TenantId.HasValue) return Results.NotFound();
        if (!user.IsActive) return Results.Conflict(new { message = "Akun sudah nonaktif." });

        user.IsActive = false;
        var update = await userManager.UpdateAsync(user);
        if (!update.Succeeded)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["User"] = update.Errors.Select(e => e.Description).ToArray() });
        }
        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(), TenantId = user.TenantId, ActorUserId = actor.UserId ?? Guid.Empty,
            Action = "SuperAdminUserDeactivated", Entity = nameof(AppUser), EntityId = user.Id.ToString(),
            MetaJson = JsonSerializer.Serialize(new { req.Reason }),
        });
        await db.SaveChangesAsync(ct);
        await sessionRevoker.RevokeUserSessionsAsync(user.Id, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ReactivateUser(
        Guid userId,
        UserManager<AppUser> userManager,
        VokasiaDbContext db,
        ITenantContext actor,
        CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && ManagedRoles.Contains(u.Role), ct);
        if (user is null || !user.TenantId.HasValue) return Results.NotFound();
        if (user.IsActive) return Results.Conflict(new { message = "Akun sudah aktif." });

        user.IsActive = true;
        var update = await userManager.UpdateAsync(user);
        if (!update.Succeeded)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["User"] = update.Errors.Select(e => e.Description).ToArray() });
        }
        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(), TenantId = user.TenantId, ActorUserId = actor.UserId ?? Guid.Empty,
            Action = "SuperAdminUserReactivated", Entity = nameof(AppUser), EntityId = user.Id.ToString(), MetaJson = "{}",
        });
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static SaUserDto ToDto(AppUser user, string tenantName) =>
        new(user.Id, user.TenantId!.Value, tenantName, user.Email ?? "", user.FullName, user.Role, user.IsActive, user.CreatedAt);

    private static List<string> AccessFor(UserRole role) => role switch
    {
        UserRole.TenantAdmin => ["Mengelola operasi tenant", "Mengelola user sekolah", "Mengakses billing tenant"],
        UserRole.DeptHead => ["Memantau penempatan dan penilaian", "Mengakses laporan sekolah"],
        UserRole.Teacher => ["Memantau siswa yang ditugaskan", "Mengisi catatan dan penilaian guru"],
        _ => [],
    };
}
