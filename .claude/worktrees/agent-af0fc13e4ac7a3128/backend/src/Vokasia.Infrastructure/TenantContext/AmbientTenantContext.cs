using Vokasia.Domain.Common;

namespace Vokasia.Infrastructure.TenantContext;

/// <summary>
/// Implementasi konkret ITenantContext, scoped per-request. Nilainya diisi
/// TenantResolutionMiddleware di Vokasia.Api (H2-E3) dari claims JWT. Untuk H1, disediakan
/// sebagai POCO settable agar DbContext & DI bisa dirakit sekarang tanpa menunggu middleware.
/// </summary>
public class AmbientTenantContext : ITenantContext
{
    public Guid? TenantId { get; set; }
    public Guid? UserId { get; set; }
    public string? Role { get; set; }
    public bool IsSuperAdminActingAsTenant { get; set; }
}
