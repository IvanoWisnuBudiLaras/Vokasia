using Microsoft.EntityFrameworkCore;
using Vokasia.Api.Auth;
using Vokasia.Api.Validation;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Api.Endpoints;

/// <summary>
/// VOK-H6-E1 §3 — /sa/plans: paket langganan GLOBAL + feature flags per plan/tenant (FR-SA-03).
/// </summary>
public static class SaPlansEndpoints
{
    /// <summary>
    /// GetEffectiveFlags "dipanggil runtime (cache 60 dtk)" — cache in-memory statis SEDERHANA
    /// (ConcurrentDictionary, bukan IMemoryCache: proyek ini belum registrasi IMemoryCache di mana
    /// pun, menambah AddMemoryCache() murni utk 1 endpoint jarang-panggil dianggap overkill dibanding
    /// dict statis + timestamp manual - NOL dependency NuGet baru, AGENTS.md #13).
    /// </summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, (DateTimeOffset ExpiresAt, Dictionary<string, bool> Flags)> EffectiveFlagsCache = new();
    private static readonly TimeSpan EffectiveFlagsCacheTtl = TimeSpan.FromSeconds(60);

    public static IEndpointRouteBuilder MapSaPlansEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/sa/plans").WithTags("SaPlans")
            .RequireAuthorization(RbacPolicies.SaOnly)
            .AddEndpointFilter<ValidationFilter>();

        group.MapPost("/", CreatePlan);
        group.MapPut("/{id:guid}", UpdatePlan);
        group.MapGet("/", ListPlans);
        group.MapPost("/{planId:guid}/flags", SetFeatureFlag);

        var tenantFlags = app.MapGroup("/sa/tenants/{tenantId:guid}/flags").WithTags("SaPlans").RequireAuthorization(RbacPolicies.SaOnly);
        tenantFlags.MapPost("/", OverrideTenantFlag);
        tenantFlags.MapGet("/effective", GetEffectiveFlags);

        return app;
    }

    private static async Task<IResult> SetFeatureFlag(Guid planId, SetFeatureFlagRequest req, VokasiaDbContext db, CancellationToken ct)
    {
        var planExists = await db.Plans.AsNoTracking().AnyAsync(p => p.Id == planId, ct);
        if (!planExists)
        {
            return Results.NotFound();
        }

        var keyStr = req.Key.ToString();
        var flag = await db.FeatureFlags.FirstOrDefaultAsync(f => f.PlanId == planId && f.Key == keyStr, ct);
        if (flag is null)
        {
            db.FeatureFlags.Add(new FeatureFlag { Id = Guid.NewGuid(), PlanId = planId, Key = keyStr, Enabled = req.Enabled });
        }
        else
        {
            flag.Enabled = req.Enabled;
        }

        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> OverrideTenantFlag(Guid tenantId, SetFeatureFlagRequest req, VokasiaDbContext db, CancellationToken ct)
    {
        var tenantExists = await db.Tenants.AsNoTracking().AnyAsync(t => t.Id == tenantId, ct);
        if (!tenantExists)
        {
            return Results.NotFound();
        }

        var keyStr = req.Key.ToString();
        var flag = await db.FeatureFlags.FirstOrDefaultAsync(f => f.TenantId == tenantId && f.Key == keyStr, ct);
        if (flag is null)
        {
            db.FeatureFlags.Add(new FeatureFlag { Id = Guid.NewGuid(), TenantId = tenantId, Key = keyStr, Enabled = req.Enabled });
        }
        else
        {
            flag.Enabled = req.Enabled;
        }

        await db.SaveChangesAsync(ct);
        EffectiveFlagsCache.TryRemove(tenantId, out _); // override baru harus terlihat SEGERA (bukan tunggu TTL 60 dtk kadaluarsa sendiri) - invalidasi eksplisit.
        return Results.NoContent();
    }

    /// <summary>AC: "resolusi plan->override" — override tenant MENANG atas nilai plan utk Key yang sama; key yang cuma ada di plan (tanpa override) ikut nilai plan apa adanya.</summary>
    private static async Task<IResult> GetEffectiveFlags(Guid tenantId, VokasiaDbContext db, CancellationToken ct)
    {
        if (EffectiveFlagsCache.TryGetValue(tenantId, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow)
        {
            return Results.Ok(cached.Flags);
        }

        var tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null)
        {
            return Results.NotFound();
        }

        var result = new Dictionary<string, bool>();
        if (tenant.PlanId.HasValue)
        {
            var planFlags = await db.FeatureFlags.AsNoTracking().Where(f => f.PlanId == tenant.PlanId).ToListAsync(ct);
            foreach (var f in planFlags)
            {
                result[f.Key] = f.Enabled;
            }
        }

        var overrides = await db.FeatureFlags.AsNoTracking().Where(f => f.TenantId == tenantId).ToListAsync(ct);
        foreach (var f in overrides)
        {
            result[f.Key] = f.Enabled; // override MENANG - ditulis belakangan, sengaja timpa nilai plan.
        }

        EffectiveFlagsCache[tenantId] = (DateTimeOffset.UtcNow.Add(EffectiveFlagsCacheTtl), result);
        return Results.Ok(result);
    }

    private static async Task<IResult> CreatePlan(PlanRequest req, VokasiaDbContext db, CancellationToken ct)
    {
        var plan = new Plan { Id = Guid.NewGuid(), Name = req.Name, PriceMonthly = req.PriceMonthly, MaxStudents = req.MaxStudents, MaxPlacements = req.MaxPlacements };
        db.Plans.Add(plan);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/sa/plans/{plan.Id}", ToDto(plan));
    }

    private static async Task<IResult> UpdatePlan(Guid id, PlanRequest req, VokasiaDbContext db, CancellationToken ct)
    {
        var plan = await db.Plans.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (plan is null)
        {
            return Results.NotFound();
        }

        plan.Name = req.Name;
        plan.PriceMonthly = req.PriceMonthly;
        plan.MaxStudents = req.MaxStudents;
        plan.MaxPlacements = req.MaxPlacements;
        await db.SaveChangesAsync(ct);
        return Results.Ok(ToDto(plan));
    }

    private static async Task<IResult> ListPlans(VokasiaDbContext db, CancellationToken ct)
    {
        var plans = await db.Plans.AsNoTracking().OrderBy(p => p.PriceMonthly).Select(p => ToDto(p)).ToListAsync(ct);
        return Results.Ok(plans);
    }

    private static PlanDto ToDto(Plan p) => new(p.Id, p.Name, p.PriceMonthly, p.MaxStudents, p.MaxPlacements);
}
