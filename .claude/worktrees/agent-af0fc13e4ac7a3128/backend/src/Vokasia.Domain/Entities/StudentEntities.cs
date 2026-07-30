using Vokasia.Domain.Common;

namespace Vokasia.Domain.Entities;

/// <summary>Jurusan sekolah (mis. TKJ) — lingkup kompetensi (FR-TEN-02).</summary>
public class Major : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = default!;
}

/// <summary>Daftar kompetensi per jurusan — dipilih siswa saat isi jurnal (FR-JRN-02).</summary>
public class Competency : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid MajorId { get; set; }
    public string Name { get; set; } = default!;
}

/// <summary>
/// Data siswa — MINIMAL by design (NFR-SEC-05, data anak). UserId nullable: siswa boleh ada
/// sebelum akun login dibuat (import CSV dulu, undang user belakangan).
/// </summary>
public class Student : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? UserId { get; set; }
    public string FullName { get; set; } = default!;
    public string? Nisn { get; set; }
    public Guid MajorId { get; set; }
    public string Classroom { get; set; } = default!;
}
