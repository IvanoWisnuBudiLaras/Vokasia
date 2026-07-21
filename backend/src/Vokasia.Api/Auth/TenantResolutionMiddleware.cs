using Vokasia.Domain.Common;
using Vokasia.Infrastructure.TenantContext;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Vokasia.Api.Auth;

/// <summary>
/// AC VOK-H2-E3: mengisi ITenantContext dari claims JWT per-request — inilah yang mengaktifkan
/// PENUH global query filter yang di-stub H1-E1 (VokasiaDbContext.ApplyTenantQueryFilters).
/// SuperAdmin boleh override lewat header X-Acting-Tenant (audit ditulis penuh di H6-E3 —
/// IAuditWriter belum ada di H2, [ASSUMPTION]: TODO ditandai eksplisit, bukan dilewatkan diam-diam).
/// </summary>
public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, AmbientTenantContext tenantContext)
    {
        var user = context.User;
        if (user.Identity?.IsAuthenticated == true)
        {
            if (Guid.TryParse(user.FindFirst(Claims.Subject)?.Value, out var userId))
            {
                tenantContext.UserId = userId;
            }

            tenantContext.Role = user.FindFirst("role")?.Value;

            if (Guid.TryParse(user.FindFirst("tenant_id")?.Value, out var tenantId))
            {
                tenantContext.TenantId = tenantId;
            }

            var isSuperAdmin = tenantContext.Role == nameof(UserRole.SuperAdmin);
            if (isSuperAdmin && context.Request.Headers.TryGetValue("X-Acting-Tenant", out var actingTenantHeader)
                && Guid.TryParse(actingTenantHeader, out var actingTenantId))
            {
                tenantContext.TenantId = actingTenantId;
                tenantContext.IsSuperAdminActingAsTenant = true;
                // TODO-H6E3: tulis AuditLog{Action="ImpersonateTenantFilter", ActorUserId, ActingAsUserId=null, MetaJson=tenant}
                // begitu IAuditWriter tersedia. Belum ada di H2 — dicatat eksplisit, bukan diam-diam (SOUL.md hierarki).
            }
        }

        await _next(context);
    }
}
