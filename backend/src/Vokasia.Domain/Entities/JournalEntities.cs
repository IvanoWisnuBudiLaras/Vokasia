using Vokasia.Domain.Common;

namespace Vokasia.Domain.Entities;

/// <summary>Slot harian ter-generate cron 05:00 WIB per placement aktif (FR-JRN-01).</summary>
public class JournalSlot : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid PlacementId { get; set; }
    public DateOnly Date { get; set; }
    public JournalSlotStatus Status { get; set; } = JournalSlotStatus.Empty;
}

/// <summary>
/// Jurnal harian siswa. IMMUTABLE setelah Approved (FR-JRN-04, NFR-SEC-08) — guard EnsureMutable()
/// ditambahkan penuh di H3-E3; struktur & properti status disiapkan sekarang.
/// </summary>
public class JournalEntry : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SlotId { get; set; }
    public Guid PlacementId { get; set; }
    public string Text { get; set; } = default!; // <=500 kar, ditegakkan FluentValidation H3-E3
    public JournalEntryStatus Status { get; set; } = JournalEntryStatus.Submitted;
    public string? MentorNote { get; set; }
    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ApprovedAt { get; set; }

    /// <summary>
    /// Domain guard immutability (FR-JRN-04, NFR-SEC-08). Dipanggil SEMUA path mutasi: update teks
    /// (resubmit), attach foto, delete — lihat JournalEndpoints.cs (SubmitJournal menolak resubmit
    /// selain saat Rejected; Approve/Reject/BatchApprove memanggil ini eksplisit sebelum mutasi).
    /// Rejected TIDAK immutable by design (AC ticket H3-E1: siswa boleh isi ulang) — hanya Approved
    /// yang terkunci permanen. Implementasi H3-E3: exception type khusus (bukan generik) supaya
    /// middleware Api bisa memetakan ke 409 + {code,message} konsisten, bukan 500.
    /// </summary>
    public void EnsureMutable()
    {
        if (Status == JournalEntryStatus.Approved)
            throw new DomainImmutableException("journal-approved-immutable", "Jurnal yang sudah disetujui tidak bisa diubah.");
    }
}

/// <summary>Foto lampiran jurnal (maks 3) — diproses async oleh PhotoUploadedConsumer di H4.</summary>
public class JournalPhoto : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid JournalEntryId { get; set; }
    public string ObjectKey { get; set; } = default!;
    public string? ThumbKey { get; set; }
    public PhotoStatus Status { get; set; } = PhotoStatus.Pending;
}

/// <summary>Relasi many-to-many jurnal ↔ kompetensi yang diklaim siswa hari itu.</summary>
public class JournalCompetency
{
    public Guid JournalEntryId { get; set; }
    public Guid CompetencyId { get; set; }
}

/// <summary>Komentar pembinaan guru pada jurnal siswa (FR-JRN-05).</summary>
public class TeacherComment : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid JournalEntryId { get; set; }
    public Guid TeacherId { get; set; }
    public string Text { get; set; } = default!;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Proyeksi status harian siswa (RAG) — sumber baca cepat dashboard W3 & ghosting detection.
/// Ditulis oleh consumer/cron H4, BUKAN dihitung ulang saat baca (hindari N+1 & query berat).
/// </summary>
public class StudentDailyStatus : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid StudentId { get; set; }
    public Guid PeriodId { get; set; }
    public DateOnly Date { get; set; }
    public RagStatus Rag { get; set; } = RagStatus.Green;
    public int Streak { get; set; }
}
