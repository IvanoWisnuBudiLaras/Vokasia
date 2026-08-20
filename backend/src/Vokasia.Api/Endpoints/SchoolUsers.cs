using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vokasia.Api.Auth;
using Vokasia.Api.Validation;
using Vokasia.Domain.Common;
using Vokasia.Infrastructure.Identity;
using Vokasia.Infrastructure.Persistence;
using Vokasia.Infrastructure.Security;
using Vokasia.Infrastructure.Email;
using Vokasia.Domain.Entities;

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
        app.MapGet("/api/staff-invitations/{token}", InspectInvitation);
        app.MapPost("/api/staff-invitations/{token}/password", SetInvitationPassword)
            .AddEndpointFilter<ValidationFilter>();

        return app;
    }

    private static async Task<IResult> InviteSchoolUser(
        InviteUserRequest req, UserManager<AppUser> userManager, ITenantContext tenant, IEmailSender emailSender, IConfiguration configuration, CancellationToken ct)
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

        // User is created without a password; the setup-link invitation activates it.
        var result = await userManager.CreateAsync(user);
        if (!result.Succeeded)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Email"] = result.Errors.Select(e => e.Description).ToArray(),
            });
        }

        // The bearer token is transient and only its hash plus UTC expiry is persisted.
        var invitation = StaffInvitationToken.Create(DateTimeOffset.UtcNow);
        await userManager.SetAuthenticationTokenAsync(user, StaffInvitationToken.LoginProvider, StaffInvitationToken.Name, StaffInvitationToken.StoredValue(invitation.Hash, invitation.ExpiresAt));
        var setupUrl = $"{(configuration["NEXT_PUBLIC_APP_URL"] ?? "http://localhost:3000").TrimEnd('/')}/set-password?token={Uri.EscapeDataString(invitation.Raw)}";
        await emailSender.SendAsync(new EmailMessage(user.Email!, "StaffInvitation", "Atur kata sandi Vokasia", $"<p>Atur kata sandi akun Vokasia Anda: <a href=\"{setupUrl}\">Atur kata sandi</a></p>", $"Atur kata sandi akun Vokasia Anda: {setupUrl}", Guid.NewGuid()), ct);
        return Results.Created($"/api/school-users/{user.Id}", new { User = ToDto(user), ExpiresInHours = 24 });
    }

    private static async Task<IResult> InspectInvitation(string token, VokasiaDbContext db, CancellationToken ct)
    {
        var record = await FindInvitationAsync(token, db, ct);
        if (record is null) return Results.NotFound(new { code = "invalid_invitation" });
        if (record.Consumed) return Results.Conflict(new { code = "used_invitation" });
        if (record.ExpiresAt <= DateTimeOffset.UtcNow) return Results.Conflict(new { code = "expired_invitation" });
        return Results.Ok(new { valid = true });
    }

    private static async Task<IResult> SetInvitationPassword(string token, SetInvitationPasswordRequest req, VokasiaDbContext db, UserManager<AppUser> userManager, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var record = await FindInvitationAsync(token, db, ct);
        if (record is null) return Results.NotFound(new { code = "invalid_invitation" });
        if (record.Consumed) return Results.Conflict(new { code = "used_invitation" });
        if (record.ExpiresAt <= DateTimeOffset.UtcNow) return Results.Conflict(new { code = "expired_invitation" });

        // The conditional update is the database arbiter. Two requests may both read a valid
        // invitation, but only one can replace the exact unconsumed value.
        var tokens = db.Set<IdentityUserToken<Guid>>();
        var consumedValue = StaffInvitationToken.ConsumedValue(record.TokenHash, record.ExpiresAt);
        var claimed = await tokens
            .Where(t => t.UserId == record.User.Id && t.LoginProvider == StaffInvitationToken.LoginProvider && t.Name == StaffInvitationToken.Name && t.Value == record.StoredValue)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.Value, consumedValue), ct);
        if (claimed != 1) return Results.Conflict(new { code = "used_invitation" });

        var result = await userManager.AddPasswordAsync(record.User, req.Password);
        if (!result.Succeeded)
        {
            await transaction.RollbackAsync(ct);
            return Results.ValidationProblem(
                result.Errors.GroupBy(e => e.Code).ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray()),
                statusCode: 422);
        }

        record.User.IsActive = true;
        db.AuditLogs.Add(new AuditLog { Id = Guid.NewGuid(), TenantId = record.User.TenantId, ActorUserId = record.User.Id, Action = "StaffInvitationConsumed", Entity = nameof(AppUser), EntityId = record.User.Id.ToString(), MetaJson = "{}" });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Results.Ok(new { activated = true });
    }

    private sealed record InvitationRecord(AppUser User, string TokenHash, string StoredValue, DateTimeOffset ExpiresAt, bool Consumed);

    private static async Task<InvitationRecord?> FindInvitationAsync(string rawToken, VokasiaDbContext db, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawToken) || rawToken.Length > 256) return null;
        var hash = StaffInvitationToken.Hash(rawToken);
        var row = await db.Set<IdentityUserToken<Guid>>().AsNoTracking().FirstOrDefaultAsync(t => t.LoginProvider == StaffInvitationToken.LoginProvider && t.Name == StaffInvitationToken.Name && t.Value != null && t.Value.StartsWith(hash + "|"), ct);
        if (row is null) return null;
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == row.UserId, ct);
        if (user is null) return null;
        var fields = row.Value!.Split('|', StringSplitOptions.None);
        if (fields.Length is < 2 or > 3 || !string.Equals(fields[0], hash, StringComparison.Ordinal) || !DateTimeOffset.TryParse(fields[1], null, System.Globalization.DateTimeStyles.RoundtripKind, out var expiresAt)) return null;
        return new InvitationRecord(user, hash, row.Value, expiresAt, fields.Length == 3 && fields[2] == "consumed");
    }

    private static async Task<IResult> AssignRole(
        Guid userId,
        [FromBody] UserRole role,
        UserManager<AppUser> userManager,
        ITenantContext tenant,
        CancellationToken ct)
    {
        if (role is not (UserRole.Teacher or UserRole.DeptHead or UserRole.TenantAdmin))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Role"] = ["Hanya Teacher, DeptHead, atau TenantAdmin yang bisa dikelola lewat endpoint ini."],
            });
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || !tenant.TenantId.HasValue || user.TenantId != tenant.TenantId)
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
