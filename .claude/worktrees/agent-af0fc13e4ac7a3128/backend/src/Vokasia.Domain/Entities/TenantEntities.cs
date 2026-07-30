namespace Vokasia.Domain.Entities;

/// <summary>Satu sekolah SMK — unit isolasi multi-tenant (PRD §1.3, FR-SA-01).</summary>
public class Tenant
{
    public Guid Id { get; set; }
    public string SchoolName { get; set; } = default!;
    public string? Npsn { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public Guid? PlanId { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// VOK-H4-E1: default false (privasi-aman, NFR-SEC-05 "data anak") - PhotoUploadedConsumer
    /// strip SELURUH EXIF (bukan cuma sub-tag GPS - lebih aman & lebih sederhana drpd bedah tag
    /// per-tag lintas format JPEG/PNG/WEBP) KECUALI flag ini true. TIDAK ADA UI toggle utk field
    /// ini di ticket manapun sampai sesi ini (dicatat DECISIONS.md) - field murni skema+consumer,
    /// diaktifkan lewat SQL/admin manual sampai ada endpoint pengaturan tenant.
    /// </summary>
    public bool GeotagAllowed { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Plan? Plan { get; set; }
}
