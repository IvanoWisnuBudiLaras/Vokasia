using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vokasia.Api.Endpoints;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Persistence;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Vokasia.Api.Auth;

public record WriteAuditLogRequest(string Action, string Entity, string EntityId, string? MetaJson);

/// <summary>
/// VOK-H2-E3 §2: <c>WriteAuditLog(actorId, actingAsId?, action, entity, entityId, metaJson)</c> —
/// satu pintu audit (FR-X-01). Diimplementasikan sebagai endpoint HTTP (bukan hanya service C#
/// dipanggil in-proc) krn pemanggil pertamanya adalah BFF Next.js (proses terpisah) utk mencatat
/// <c>TokenReuseDetected</c> saat refresh rotation gagal (lib/refresh.ts). Actor diambil dari
/// claim token yang memanggil endpoint ini (BUKAN dari body request) — mencegah siapa pun
/// mengaku sebagai actor lain.
/// </summary>
public static class AuditEndpoints
{
    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder app)
    {
        // RequireAuthorization() tanpa policy tertentu = token valid apa pun cukup (siapa saja yg
        // terautentikasi boleh mencatat audit ATAS DIRINYA sendiri — bukan atas user lain).
        app.MapPost("/api/audit", WriteAudit).RequireAuthorization();

        // VOK-H6-E1 §4 (FR-SA-07): "TenantAdmin versi tenant-scoped (endpoint terpisah policy
        // TenantAdmin)" — pasangan QueryAuditLogs SA (SaOpsEndpoints, semua tenant) di sini HANYA
        // tenant sendiri (TenantId dari claim, BUKAN dari query string — TenantAdmin tak boleh
        // mengintip audit tenant lain lewat parameter).
        app.MapGet("/api/audit-logs", GetTenantAuditLogs).RequireAuthorization(Api.Auth.RbacPolicies.TenantAdminOnly);

        return app;
    }

    private static async Task<IResult> GetTenantAuditLogs(
        VokasiaDbContext db, ITenantContext tenant, CancellationToken ct,
        [FromQuery] Guid? actorId = null, [FromQuery] string? entity = null,
        [FromQuery] DateTimeOffset? from = null, [FromQuery] DateTimeOffset? to = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (!tenant.TenantId.HasValue)
        {
            return Results.Forbid();
        }

        var query = db.AuditLogs.AsNoTracking().Where(a => a.TenantId == tenant.TenantId);
        if (actorId.HasValue) query = query.Where(a => a.ActorUserId == actorId.Value);
        if (!string.IsNullOrWhiteSpace(entity)) query = query.Where(a => a.Entity == entity);
        if (from.HasValue) query = query.Where(a => a.CreatedAt >= from.Value);
        if (to.HasValue) query = query.Where(a => a.CreatedAt <= to.Value);

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(a => a.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new AuditDto(a.Id, a.TenantId, a.ActorUserId, a.ActingAsUserId, a.Action, a.Entity, a.EntityId, a.MetaJson, a.CreatedAt))
            .ToListAsync(ct);

        return Results.Ok(new Paged<AuditDto>(items, page, pageSize, total));
    }

    private static async Task<IResult> WriteAudit(
        WriteAuditLogRequest req, HttpContext ctx, VokasiaDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        var sub = ctx.User.FindFirst(Claims.Subject)?.Value;
        if (!Guid.TryParse(sub, out var actorId))
        {
            return Results.Unauthorized();
        }

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.TenantId,
            ActorUserId = actorId,
            Action = req.Action,
            Entity = req.Entity,
            EntityId = req.EntityId,
            MetaJson = req.MetaJson ?? "{}",
        });
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    }
}
