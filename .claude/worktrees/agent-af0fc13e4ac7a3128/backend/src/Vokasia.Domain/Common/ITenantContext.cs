namespace Vokasia.Domain.Common;

/// <summary>
/// Konteks tenant per-request. Sumber tunggal kebenaran untuk global query filter (FR-AUTH-06).
/// Implementasi konkret (dari claims JWT) diisi middleware di Vokasia.Api pada H2-E3.
/// </summary>
public interface ITenantContext
{
    Guid? TenantId { get; }
    Guid? UserId { get; }
    string? Role { get; }

    /// <summary>True bila SuperAdmin sedang mem-bypass filter tenant lewat header X-Acting-Tenant (H2-E3).</summary>
    bool IsSuperAdminActingAsTenant { get; }
}

/// <summary>Ditandai pada entitas yang wajib difilter per tenant oleh EF global query filter.</summary>
public interface ITenantScoped
{
    Guid TenantId { get; set; }
}
