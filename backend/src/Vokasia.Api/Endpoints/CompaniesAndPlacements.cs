using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vokasia.Api.Auth;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Api.Endpoints;

/// <summary>VOK-H2-E1 §5: link/propose DUDI ke tenant, kuota slot, dan placement.</summary>
public static class CompaniesAndPlacementsEndpoints
{
    public static IEndpointRouteBuilder MapCompaniesAndPlacementsEndpoints(this IEndpointRouteBuilder app)
    {
        var companies = app.MapGroup("/api/companies").WithTags("Companies");
        companies.MapPost("/link/{companyId:guid}", LinkCompanyToTenant).RequireAuthorization(RbacPolicies.TenantAdminOnly);
        companies.MapPost("/propose", ProposeCompany).RequireAuthorization(RbacPolicies.TenantAdminOnly);
        companies.MapPost("/{companyId:guid}/periods/{periodId:guid}/slots", SetCompanySlots).RequireAuthorization(RbacPolicies.DeptHeadPlus);

        var placements = app.MapGroup("/api/placements").WithTags("Placements");
        placements.MapPost("/", CreatePlacement).RequireAuthorization(RbacPolicies.DeptHeadPlus);
        placements.MapPost("/bulk", BulkCreatePlacements).RequireAuthorization(RbacPolicies.DeptHeadPlus);
        placements.MapPut("/{id:guid}/teacher/{teacherId:guid}", AssignTeacher).RequireAuthorization(RbacPolicies.DeptHeadPlus);
        placements.MapGet("/", ListPlacements).RequireAuthorization(RbacPolicies.TenantMember);
        placements.MapGet("/{id:guid}", GetPlacement).RequireAuthorization(RbacPolicies.TenantMember);

        return app;
    }

    private static async Task<IResult> LinkCompanyToTenant(Guid companyId, VokasiaDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        if (!tenant.TenantId.HasValue)
        {
            return Results.Forbid();
        }

        var exists = await db.Companies.AnyAsync(c => c.Id == companyId, ct);
        if (!exists)
        {
            return Results.NotFound();
        }

        var alreadyLinked = await db.TenantCompanies.AnyAsync(tc => tc.TenantId == tenant.TenantId && tc.CompanyId == companyId, ct);
        if (!alreadyLinked)
        {
            db.TenantCompanies.Add(new TenantCompany { TenantId = tenant.TenantId.Value, CompanyId = companyId });
            await db.SaveChangesAsync(ct);
        }

        return Results.NoContent();
    }

    private static async Task<IResult> ProposeCompany(ProposeCompanyRequest req, VokasiaDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        var company = new Company
        {
            Id = Guid.NewGuid(),
            Name = req.Name,
            Sector = req.Sector,
            City = req.City,
            Address = req.Address,
            ContactPerson = req.ContactPerson,
            IsVerified = false, // verifikasi oleh SuperAdmin, H6-E1.
        };
        db.Companies.Add(company);

        if (tenant.TenantId.HasValue)
        {
            db.TenantCompanies.Add(new TenantCompany { TenantId = tenant.TenantId.Value, CompanyId = company.Id });
        }

        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/companies/{company.Id}", ToDto(company));
    }

    private static async Task<IResult> SetCompanySlots(
        Guid companyId, Guid periodId, [FromBody] int slots, VokasiaDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        if (!tenant.TenantId.HasValue)
        {
            return Results.Forbid();
        }

        var slot = await db.CompanySlots.FirstOrDefaultAsync(
            s => s.TenantId == tenant.TenantId && s.CompanyId == companyId && s.PeriodId == periodId, ct);

        if (slot is null)
        {
            db.CompanySlots.Add(new CompanySlot { Id = Guid.NewGuid(), TenantId = tenant.TenantId.Value, CompanyId = companyId, PeriodId = periodId, Slots = slots });
        }
        else
        {
            slot.Slots = slots;
        }

        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    /// <summary>Stub kuota BILLING (plan.MaxPlacements) — TODO-H6-E1. Kuota SLOT per-DUDI (CompanySlot) tetap ditegakkan sekarang.</summary>
    private static bool CheckQuotaOnPlacement_TODO_H6(Guid tenantId) => true;

    private static async Task<(bool Ok, string? Error)> TryReserveSlot(VokasiaDbContext db, Guid companyId, Guid periodId, CancellationToken ct)
    {
        var slot = await db.CompanySlots.AsNoTracking().FirstOrDefaultAsync(s => s.CompanyId == companyId && s.PeriodId == periodId, ct);
        if (slot is null)
        {
            return (true, null); // belum ada kuota diset = tanpa batas (ASSUMPTION MVP).
        }

        var used = await db.Placements.CountAsync(p => p.CompanyId == companyId && p.PeriodId == periodId && p.Status == PlacementStatus.Active, ct);
        return used >= slot.Slots ? (false, "Slot DUDI untuk periode ini sudah penuh.") : (true, null);
    }

    private static async Task<IResult> CreatePlacement(CreatePlacementRequest req, VokasiaDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        if (!tenant.TenantId.HasValue)
        {
            return Results.Forbid();
        }

        var (ok, error) = await TryReserveSlot(db, req.CompanyId, req.PeriodId, ct);
        if (!ok)
        {
            return Results.Conflict(new { message = error });
        }

        CheckQuotaOnPlacement_TODO_H6(tenant.TenantId.Value);

        var placement = new Placement
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.TenantId.Value,
            StudentId = req.StudentId,
            CompanyId = req.CompanyId,
            PeriodId = req.PeriodId,
            TeacherId = req.TeacherId,
            MentorEmail = req.MentorEmail,
            Status = PlacementStatus.Active,
        };
        db.Placements.Add(placement);

        // AC: OutboxMessage{PlacementCreated} tercatat 1 transaksi dgn placement (dispatcher nyata H4-E1).
        db.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = "PlacementCreated",
            PayloadJson = JsonSerializer.Serialize(new { placement.Id, placement.StudentId, placement.CompanyId, placement.PeriodId, placement.MentorEmail }),
        });

        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/placements/{placement.Id}", ToDto(placement));
    }

    private static async Task<IResult> BulkCreatePlacements(List<CreatePlacementRequest> reqs, VokasiaDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        if (!tenant.TenantId.HasValue)
        {
            return Results.Forbid();
        }

        var successIds = new List<Guid>();
        var errors = new List<ImportRowError>();

        for (var i = 0; i < reqs.Count; i++)
        {
            var req = reqs[i];
            var (ok, error) = await TryReserveSlot(db, req.CompanyId, req.PeriodId, ct);
            if (!ok)
            {
                errors.Add(new ImportRowError(i, "CompanyId", error!));
                continue;
            }

            var placement = new Placement
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.TenantId.Value,
                StudentId = req.StudentId,
                CompanyId = req.CompanyId,
                PeriodId = req.PeriodId,
                TeacherId = req.TeacherId,
                MentorEmail = req.MentorEmail,
                Status = PlacementStatus.Active,
            };
            db.Placements.Add(placement);
            db.OutboxMessages.Add(new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Type = "PlacementCreated",
                PayloadJson = JsonSerializer.Serialize(new { placement.Id, placement.StudentId, placement.CompanyId, placement.PeriodId }),
            });
            successIds.Add(placement.Id);
        }

        await db.SaveChangesAsync(ct);
        return Results.Ok(new BulkResult(successIds, errors));
    }

    private static async Task<IResult> AssignTeacher(Guid id, Guid teacherId, VokasiaDbContext db, CancellationToken ct)
    {
        var placement = await db.Placements.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (placement is null)
        {
            return Results.NotFound();
        }

        placement.TeacherId = teacherId;
        await db.SaveChangesAsync(ct);
        return Results.Ok(ToDto(placement));
    }

    private static async Task<IResult> ListPlacements(
        VokasiaDbContext db, CancellationToken ct,
        [FromQuery] Guid periodId, [FromQuery] Guid? companyId = null, [FromQuery] PlacementStatus? status = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var query = db.Placements.AsNoTracking().Where(p => p.PeriodId == periodId);
        if (companyId.HasValue)
        {
            query = query.Where(p => p.CompanyId == companyId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(p => p.Status == status.Value);
        }

        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(p => ToDto(p)).ToListAsync(ct);

        return Results.Ok(new Paged<PlacementDto>(items, page, pageSize, total));
    }

    private static async Task<IResult> GetPlacement(Guid id, VokasiaDbContext db, CancellationToken ct)
    {
        var placement = await db.Placements.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        return placement is null ? Results.NotFound() : Results.Ok(ToDto(placement));
    }

    private static CompanyDto ToDto(Company c) => new(c.Id, c.Name, c.Sector, c.City, c.Address, c.ContactPerson, c.IsVerified);
    private static PlacementDto ToDto(Placement p) => new(p.Id, p.StudentId, p.CompanyId, p.PeriodId, p.TeacherId, p.MentorUserId, p.Status);
}
