namespace Vokasia.Domain.Common;

/// <summary>Peran user sesuai matrix RBAC PRD 2.3. Disimpan sebagai string di DB (lihat DbContext).</summary>
public enum UserRole
{
    SuperAdmin,
    TenantAdmin,
    DeptHead,
    Teacher,
    IndustryMentor,
    Student,
    ParentViewer
}

public enum PeriodStatus { Draft, Active, Assessment, Closed }

public enum PlacementStatus { Active, Completed, Terminated }

public enum JournalSlotStatus { Empty, Filled }

public enum JournalEntryStatus { Submitted, Approved, Rejected }

public enum PhotoStatus { Pending, Processed, Failed }

/// <summary>Red-Amber-Green — status harian siswa dipakai dashboard W3 & ghosting detection (FR-JRN-07).</summary>
public enum RagStatus { Green, Amber, Red }

public enum RubricAspectKind { Teknis, Softskill, Kehadiran }

public enum ScoredBy { Mentor, Teacher }

public enum InvoiceStatus { Issued, ProofUploaded, Paid }

/// <summary>VOK-H5-E1 §4 — format export rekap nilai (FR-ASM-06). Dipakai request DTO (Api) + ExportRequest entity + Worker consumer.</summary>
public enum ExportFormat { Xlsx, Pdf }

public enum ExportStatus { Requested, Completed, Failed }

/// <summary>
/// VOK-H6-E1 §3 — key FeatureFlag terdaftar (FR-SA-03, ticket literal: "key terdaftar enum
/// (GeotagAllowed, ParentDigest, ...)"). [GAP dicatat eksplisit, bukan diam-diam]: "GeotagAllowed"
/// di sini ADALAH nama yang SAMA dgn kolom <c>Tenant.GeotagAllowed</c> (H4-E1) yang SUDAH dibaca
/// langsung oleh PhotoUploadedConsumer sejak H4-E1 — keduanya TIDAK disatukan (mekanisme FeatureFlag
/// generik H6-E1 ini TIDAK menulis/membaca kolom Tenant.GeotagAllowed, dan sebaliknya): skema beku
/// gate M0 (PRD 3.6 change control) melarang mengubah kolom yang sudah ada tanpa keputusan Dev
/// eksplisit. Dua mekanisme "GeotagAllowed" hidup berdampingan sengaja, bukan bug — dicatat di
/// DECISIONS.md sbg keputusan yang perlu ditinjau (kandidat konsolidasi ticket masa depan).
/// ParentDigest = placeholder FR baru (belum ada consumer/pemanggil apa pun sampai ticket ini,
/// murni tersedia sbg key yang bisa di-flag SA - pola sama ExportReady/InvoiceIssued "dirender tapi
/// belum dipanggil" di EmailTemplateRenderer sebelum consumer-nya ada).
/// </summary>
public enum FeatureFlagKey { GeotagAllowed, ParentDigest }

/// <summary>
/// VOK-H4-E1 — tipe notifikasi in-app (Notification.Type disimpan sbg ToString() nilai ini, string
/// biasa di DB - konsisten dgn nilai string yang SUDAH dipakai H3-E1/cron sebelum enum ini ada:
/// "TeacherComment", "JournalReminder" - nama member di bawah SENGAJA sama persis suapya baris lama
/// tetap valid/cocok tanpa migrasi data).
/// </summary>
public enum NotificationType
{
    JournalApproved,
    JournalRejected,
    GhostingAlert,
    TeacherComment,
    JournalReminder,
    PhotoProcessingFailed,
    MentorWelcome,
    PlacementWelcome,
    /// <summary>VOK-H5-E1 §3 — OpenAssessmentPhase cron: periode masuk fase penilaian (H-14 dari EndDate).</summary>
    AssessmentPhaseOpened,
    /// <summary>VOK-H5-E1 §4 — ExportRequestedConsumer: file rekap (Xlsx/Pdf) siap diunduh (presigned 24 jam).</summary>
    ExportReady,
}
