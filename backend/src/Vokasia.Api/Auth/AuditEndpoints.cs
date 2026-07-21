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
        return app;
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
