using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vokasia.Api.Auth;
using Vokasia.Api.Validation;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Identity;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Api.Endpoints;

/// <summary>
/// VOK-H6-E1 §1 — /sa/tenants: wizard provisioning + CRUD tenant. Semua endpoint policy `SaOnly`
/// (ticket: "Semua endpoint /sa/* policy SaOnly"). Prioritas #1 ticket (gate M5).
/// </summary>
public static class SaTenantsEndpoints
{
    public static IEndpointRouteBuilder MapSaTenantsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/sa/tenants").WithTags("SaTenants")
            .RequireAuthorization(RbacPolicies.SaOnly)
            .AddEndpointFilter<ValidationFilter>();

        group.MapPost("/", CreateTenant);
        group.MapPut("/{id:guid}", UpdateTenant);
        group.MapGet("/", ListTenants);
        group.MapGet("/{id:guid}", GetTenant);
        group.MapPost("/{id:guid}/deactivate", DeactivateTenant);

        return app;
    }

    /// <summary>
    /// AC gate M5: "admin baru bisa login & rubrik default ada". Satu transaksi EF (BeginTransactionAsync)
    /// membungkus Tenant + RubricTemplate default + AppUser TenantAdmin (via UserManager, DbContext SAMA
    /// - lihat DependencyInjection.cs, UserManager<AppUser> & VokasiaDbContext scoped ke request yang
    /// sama) + OutboxMessage{TenantAdminInvited} + AuditLog — rollback penuh kalau salah satu gagal
    /// (mis. email admin sudah dipakai) TERMASUK Tenant/RubricTemplate yang sudah ditulis sebelumnya.
    /// </summary>
    private static async Task<IResult> CreateTenant(
        CreateTenantRequest req, VokasiaDbContext db, UserManager<AppUser> userManager, ITenantContext actingUser, CancellationToken ct)
    {
        var planExists = await db.Plans.AsNoTracking().AnyAsync(p => p.Id == req.PlanId, ct);
        if (!planExists)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["PlanId"] = ["Plan tidak ditemukan."] });
        }

        var existingEmail = await userManager.FindByEmailAsync(req.AdminEmail);
        if (existingEmail is not null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["AdminEmail"] = ["Email sudah dipakai akun lain."] });
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            SchoolName = req.SchoolName,
            Npsn = req.Npsn,
            City = req.City,
            PlanId = req.PlanId,
            IsActive = true,
        };
        db.Tenants.Add(tenant);

        // Rubrik default "Kurikulum Merdeka" (AC literal: "rubrik default ada") — bobot Σ=100
        // (invariant yang sama ditegakkan CreateRubric/UpdateRubric, lihat RubricEndpoints.WeightsSumTo100).
        var rubric = new RubricTemplate
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Name = "Kurikulum Merdeka (Default)",
            IsDefault = true,
            Aspects =
            [
                new RubricAspect { Id = Guid.NewGuid(), Name = "Kompetensi Teknis", Kind = RubricAspectKind.Teknis, Weight = 60 },
                new RubricAspect { Id = Guid.NewGuid(), Name = "Soft Skill", Kind = RubricAspectKind.Softskill, Weight = 30 },
                new RubricAspect { Id = Guid.NewGuid(), Name = "Kehadiran", Kind = RubricAspectKind.Kehadiran, Weight = 10 },
            ],
        };
        db.RubricTemplates.Add(rubric);

        await db.SaveChangesAsync(ct);

        // Password sementara acak — pola SAMA dgn SchoolUsersEndpoints.InviteSchoolUser (H2-E1),
        // kini benar2 terkirim lewat email (TenantAdminInvitedConsumer) krn infra email sudah ada (H4-E3).
        var tempPassword = $"Tmp-{Guid.NewGuid():N}Aa1!";
        var adminUser = new AppUser
        {
            UserName = req.AdminEmail,
            Email = req.AdminEmail,
            FullName = req.AdminName,
            TenantId = tenant.Id,
            Role = UserRole.TenantAdmin,
        };
        var createResult = await userManager.CreateAsync(adminUser, tempPassword);
        if (!createResult.Succeeded)
        {
            await tx.RollbackAsync(ct);
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["AdminEmail"] = createResult.Errors.Select(e => e.Description).ToArray(),
            });
        }

        db.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = "TenantAdminInvited",
            PayloadJson = JsonSerializer.Serialize(new { TenantId = tenant.Id, UserId = adminUser.Id, Email = req.AdminEmail, FullName = req.AdminName, TempPassword = tempPassword }),
        });

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            ActorUserId = actingUser.UserId ?? Guid.Empty,
            Action = "TenantCreated",
            Entity = nameof(Tenant),
            EntityId = tenant.Id.ToString(),
            MetaJson = JsonSerializer.Serialize(new { tenant.SchoolName, req.PlanId }),
        });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Results.Created($"/sa/tenants/{tenant.Id}", ToDto(tenant));
    }

    private static async Task<IResult> UpdateTenant(Guid id, UpdateTenantRequest req, VokasiaDbContext db, CancellationToken ct)
    {
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (tenant is null)
        {
            return Results.NotFound();
        }

        if (req.PlanId.HasValue)
        {
            var planExists = await db.Plans.AsNoTracking().AnyAsync(p => p.Id == req.PlanId.Value, ct);
            if (!planExists)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["PlanId"] = ["Plan tidak ditemukan."] });
            }
        }

        tenant.SchoolName = req.SchoolName;
        tenant.Npsn = req.Npsn;
        tenant.Address = req.Address;
        tenant.City = req.City;
        if (req.PlanId.HasValue)
        {
            tenant.PlanId = req.PlanId.Value;
        }

        await db.SaveChangesAsync(ct);
        return Results.Ok(ToDto(tenant));
    }

    private static async Task<IResult> ListTenants(
        VokasiaDbContext db, CancellationToken ct,
        [FromQuery] string? search = null, [FromQuery] Guid? planId = null, [FromQuery] bool? active = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var query = db.Tenants.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(t => t.SchoolName.Contains(search) || (t.Npsn != null && t.Npsn.Contains(search)));
        }

        if (planId.HasValue)
        {
            query = query.Where(t => t.PlanId == planId.Value);
        }

        if (active.HasValue)
        {
            query = query.Where(t => t.IsActive == active.Value);
        }

        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(t => t.SchoolName).Skip((page - 1) * pageSize).Take(pageSize).Select(t => ToDto(t)).ToListAsync(ct);

        return Results.Ok(new Paged<TenantDto>(items, page, pageSize, total));
    }

    private static async Task<IResult> GetTenant(Guid id, VokasiaDbContext db, CancellationToken ct)
    {
        var tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
        if (tenant is null)
        {
            return Results.NotFound();
        }

        var studentCount = await db.Students.AsNoTracking().CountAsync(s => s.TenantId == id, ct);
        var activePlacementCount = await db.Placements.AsNoTracking().CountAsync(p => p.TenantId == id && p.Status == PlacementStatus.Active, ct);
        var staffCount = await db.Users.AsNoTracking().CountAsync(u => u.TenantId == id, ct);

        var stats = new TenantStatsDto(studentCount, activePlacementCount, staffCount);
        return Results.Ok(new TenantDetailDto(ToDto(tenant), stats));
    }

    /// <summary>
    /// AC: "nonaktif -> semua session user tenant dicabut (hook H2-E3) + placement baru terblokir;
    /// data TIDAK dihapus." Hook revocation Redis SENDIRI belum ada di manapun sampai sesi ini
    /// (SchoolUsersEndpoints.DeactivateUser sudah catat TODO-H2E3 yang sama, gap dipertahankan
    /// konsisten - BUKAN pura-pura selesai). "Placement baru terblokir" DITEGAKKAN nyata: lihat
    /// CompaniesAndPlacementsEndpoints.CreatePlacement (cek Tenant.IsActive ditambahkan H6-E1).
    /// </summary>
    private static async Task<IResult> DeactivateTenant(Guid id, DeactivateTenantRequest req, VokasiaDbContext db, ITenantContext actingUser, CancellationToken ct)
    {
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (tenant is null)
        {
            return Results.NotFound();
        }

        tenant.IsActive = false;

        var tenantUsers = await db.Users.Where(u => u.TenantId == id).ToListAsync(ct);
        foreach (var u in tenantUsers)
        {
            u.IsActive = false;
            // TODO-H2E3 (gap sama persis SchoolUsersEndpoints.DeactivateUser): cabut session Redis
            // user ini instan begitu revocation store tersedia.
        }

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = id,
            ActorUserId = actingUser.UserId ?? Guid.Empty,
            Action = "TenantDeactivated",
            Entity = nameof(Tenant),
            EntityId = id.ToString(),
            MetaJson = JsonSerializer.Serialize(new { req.Reason }),
        });

        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static TenantDto ToDto(Tenant t) => new(t.Id, t.SchoolName, t.Npsn, t.City, t.Address, t.PlanId, t.IsActive, t.CreatedAt);
}
