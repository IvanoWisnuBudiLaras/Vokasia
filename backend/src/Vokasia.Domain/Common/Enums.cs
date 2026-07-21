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
