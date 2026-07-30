using Vokasia.Infrastructure.TenantContext;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Vokasia.Api.Auth;

/// <summary>
/// AC VOK-H2-E3: mengisi ITenantContext hanya dari claims JWT per-request — inilah yang mengaktifkan
/// PENUH global query filter yang di-stub H1-E1 (VokasiaDbContext.ApplyTenantQueryFilters).
/// SuperAdmin yang perlu bertindak sebagai pengguna lain wajib memakai alur impersonation teraudit.
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

            // VOK-H6-E3 §1: token hasil StartImpersonation (AuthorizationController.Exchange(),
            // custom grant ImpersonationGrantType) membawa claim "impersonator_id" = UserId SuperAdmin
            // ASLI, SEMENTARA sub/role/tenant_id di atas sudah 100% milik user TARGET (identity tertukar
            // penuh, bukan cuma filter). VokasiaDbContext.SaveChangesAsync membaca field ini utk menegakkan
            // AC "audit log mencatat actor=SA, as=user" secara otomatis di SETIAP AuditLog.Add manapun.
            if (Guid.TryParse(user.FindFirst("impersonator_id")?.Value, out var impersonatorId))
            {
                tenantContext.ImpersonatorUserId = impersonatorId;
            }
        }

        await _next(context);
    }
}
