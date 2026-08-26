using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vokasia.Api.Auth;
using Vokasia.Api.Authorization;
using Vokasia.Api.Validation;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Api.Endpoints;

/// <summary>VOK-H2-E1 §2: Periode PKL tenant. Mutasi = DeptHead+ (DeptHead atau TenantAdmin); baca = TenantMember.</summary>
public static class PeriodsEndpoints
{
    public static IEndpointRouteBuilder MapPeriodsEndpoints(this IEndpointRouteBuilder app)
    {
        // VOK-H3-E3 §2: ValidationFilter global - CreatePeriodValidator menegakkan Start<End+ClassLevels
        // utk CreatePeriodRequest; UpdatePeriodRequest BELUM punya validator terdaftar (di luar daftar
        // 8 named ticket) - inline check StartDate>=EndDate di UpdatePeriod() SENGAJA dipertahankan,
        // dicatat sbg gap eksplisit di DECISIONS.md (bukan celah diam-diam).
        var group = app.MapGroup("/api/periods").WithTags("Periods").AddEndpointFilter<ValidationFilter>();

        group.MapPost("/", CreatePeriod).RequireAuthorization(RbacPolicies.DeptHeadPlus);
        group.MapPut("/{id:guid}", UpdatePeriod).RequireAuthorization(RbacPolicies.DeptHeadPlus);
        group.MapGet("/", ListPeriods).RequireAuthorization(RbacPolicies.TenantMember);
        group.MapPut("/{id:guid}/holidays", SetHolidayCalendar).RequireAuthorization(RbacPolicies.DeptHeadPlus);

        return app;
    }

    private static async Task<IResult> CreatePeriod(
        CreatePeriodRequest req, VokasiaDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        // VOK-H3-E3 §2: Start<End + ClassLevels ⊆ {X,XI,XII} sekarang di CreatePeriodValidator (ValidationFilter global).
        if (!tenant.TenantId.HasValue)
        {
            return Results.Forbid();
        }

        var period = new Period
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.TenantId.Value,
            Name = req.Name,
            StartDate = req.StartDate,
            EndDate = req.EndDate,
            ClassLevels = string.Join(",", req.ClassLevels),
            Status = PeriodStatus.Draft,
        };
        db.Periods.Add(period);

        if (req.Holidays is { Count: > 0 })
        {
            foreach (var h in req.Holidays)
            {
                db.Holidays.Add(new Holiday { Id = Guid.NewGuid(), TenantId = tenant.TenantId.Value, PeriodId = period.Id, Date = h.Date, Label = h.Label });
            }
        }

        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/periods/{period.Id}", ToDto(period));
    }

    private static async Task<IResult> UpdatePeriod(
        Guid id, UpdatePeriodRequest req, VokasiaDbContext db, CancellationToken ct)
    {
        var period = await db.Periods.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (period is null)
        {
            return Results.NotFound();
        }

        if (period.Status == PeriodStatus.Closed)
        {
            return Results.Conflict(new { message = "Periode Closed tidak bisa diubah." });
        }

        if (req.StartDate >= req.EndDate)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["StartDate"] = ["StartDate harus sebelum EndDate."],
            });
        }

        period.Name = req.Name;
        period.StartDate = req.StartDate;
        period.EndDate = req.EndDate;
        period.ClassLevels = string.Join(",", req.ClassLevels);
        await db.SaveChangesAsync(ct);

        return Results.Ok(ToDto(period));
    }

    private static async Task<IResult> ListPeriods(
        VokasiaDbContext db, CancellationToken ct, [FromQuery] PeriodStatus? status = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var query = db.Periods.AsNoTracking().AsQueryable();
        if (status.HasValue)
        {
            query = query.Where(p => p.Status == status.Value);
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(p => p.StartDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => ToDto(p))
            .ToListAsync(ct);

        return Results.Ok(new Paged<PeriodDto>(items, page, pageSize, total));
    }

    private static async Task<IResult> SetHolidayCalendar(
        Guid id, List<HolidayDto> holidays, VokasiaDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        var period = await db.Periods.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (period is null)
        {
            return Results.NotFound();
        }

        var existing = db.Holidays.Where(h => h.PeriodId == id);
        db.Holidays.RemoveRange(existing);

        foreach (var h in holidays)
        {
            db.Holidays.Add(new Holiday { Id = Guid.NewGuid(), TenantId = tenant.TenantId!.Value, PeriodId = id, Date = h.Date, Label = h.Label });
        }

        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static PeriodDto ToDto(Period p) => new(p.Id, p.Name, p.StartDate, p.EndDate, p.ClassLevels, p.Status);
}
