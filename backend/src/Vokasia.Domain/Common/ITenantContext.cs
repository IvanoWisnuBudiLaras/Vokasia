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

    /// <summary>
    /// VOK-H6-E3: UserId SuperAdmin ASLI bila request ini memakai access token hasil StartImpersonation
    /// (claim "impersonator_id") — null dalam kondisi normal. Identitas request ditukar penuh menjadi
    /// identitas user target (role/tenant_id/UserId semuanya milik target), lalu field ini dipakai
    /// VokasiaDbContext.SaveChangesAsync untuk menegakkan AC "audit log mencatat actor=SA, as=user"
    /// tanpa menyentuh setiap endpoint yang sudah menulis AuditLog.
    /// </summary>
    Guid? ImpersonatorUserId { get; }
}

/// <summary>Ditandai pada entitas yang wajib difilter per tenant oleh EF global query filter.</summary>
public interface ITenantScoped
{
    Guid TenantId { get; set; }
}
