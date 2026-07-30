using Vokasia.Domain.Common;

namespace Vokasia.Domain.Entities;

/// <summary>Sertifikat PDF ber-QR (FR-CRT-01). CertCode = kode publik untuk /verify/{code}, bukan Id.</summary>
public class Certificate : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid PlacementId { get; set; }
    public string CertCode { get; set; } = default!; // random 12 kar url-safe, unik
    public string PdfKey { get; set; } = default!;
    public DateTimeOffset IssuedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Portofolio publik opt-in siswa (FR-CRT-03). Slug unik global. Publik hanya membaca lewat
/// proyeksi tanpa kontak/NISN (NFR-SEC-05) — ditegakkan di endpoint H6, bukan di entitas ini.
/// </summary>
public class Portfolio : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid StudentId { get; set; }
    public string? Slug { get; set; }
    public string? Headline { get; set; }
    public bool IsPublished { get; set; }
    /// <summary>Id JournalEntry approved yang dikurasi siswa sebagai sampel — csv sederhana untuk MVP.</summary>
    public string SampleJournalIdsCsv { get; set; } = "";
}
