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
