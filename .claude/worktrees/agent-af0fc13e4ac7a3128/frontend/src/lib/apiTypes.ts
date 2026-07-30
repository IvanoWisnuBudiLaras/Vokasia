/**
 * Bentuk minimal DTO backend yang benar-benar dipakai FE — cermin sebagian
 * Vokasia.Api/Endpoints/Dtos.cs (camelCase, System.Text.Json default policy ASP.NET Core).
 * Sengaja TIDAK menyalin seluruh kontrak backend field-demi-field: tambah field di sini hanya
 * saat ada halaman FE yang benar memakainya, supaya tidak ada tipe basi yang diam-diam menyimpang
 * dari DTO asli C#.
 */

export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface PeriodSummary {
  id: string;
  name: string;
  startDate: string;
  status: "Draft" | "Active" | "Assessment" | "Closed";
}

/**
 * VOK-H3-E2 — DTO jurnal (cermin Vokasia.Api/Endpoints/Dtos.cs enum-terkait). PENTING: backend
 * TIDAK mengonfigurasi JsonStringEnumConverter di mana pun (dikonfirmasi grep Program.cs/
 * DependencyInjection.cs, sama temuan yang memaksa test C# pakai .GetInt32() bukan .GetString() —
 * lihat DECISIONS.md D24) — semua enum di bawah datang sbg ANGKA MENTAH dari API, BUKAN string.
 * Konstanta numerik di bawah cermin urutan deklarasi enum C# persis (Enums.cs) — jangan diubah
 * urutannya tanpa mengubah C# juga.
 */
export const JournalSlotStatus = { Empty: 0, Filled: 1 } as const;
export const JournalEntryStatus = { Submitted: 0, Approved: 1, Rejected: 2 } as const;
export const PhotoStatus = { Pending: 0, Processed: 1, Failed: 2 } as const;

export interface CompetencyDto {
  id: string;
  name: string;
  majorId: string;
}

export interface JournalSlotDto {
  id: string;
  date: string;
  status: number; // JournalSlotStatus
}

export interface WeekDayStatusDto {
  date: string;
  status: number; // JournalSlotStatus
}

export interface PhotoDto {
  id: string;
  objectKey: string;
  thumbKey: string | null;
  status: number; // PhotoStatus
}

export interface JournalDto {
  id: string;
  slotId: string;
  placementId: string;
  text: string;
  status: number; // JournalEntryStatus
  mentorNote: string | null;
  submittedAt: string;
  approvedAt: string | null;
  photos: PhotoDto[];
  competencyIds: string[];
}

export interface TodayJournalDto {
  slot: JournalSlotDto;
  entry: JournalDto | null;
  competencies: CompetencyDto[];
  weekStatus: WeekDayStatusDto[];
  streak: number;
}

export interface PresignedUploadDto {
  uploadUrl: string;
  objectKey: string;
  expiresIn: number;
}

export interface PendingGroupDto {
  studentId: string;
  studentFullName: string;
  entries: JournalDto[];
}

export interface BatchFailure {
  id: string;
  reason: string;
}

export interface BatchResult {
  approved: string[];
  failed: BatchFailure[];
}

/**
 * VOK-H4-E1/E2 — dashboard sekolah (W3) + notifikasi in-app. Rag numerik mengikuti pola yang sama
 * dengan JournalSlotStatus dkk di atas (tanpa JsonStringEnumConverter, lihat komentar file ini).
 */
export const RagStatus = { Green: 0, Amber: 1, Red: 2 } as const;

/** Numerik (backend) -> literal string dipakai <StatusBadge> (components/ui/StatusBadge.tsx punya tipe RagStatus SENDIRI yang beda — string literal, bukan angka — sengaja TIDAK disatukan nama tipenya, lihat pemakaian). */
export function ragToBadgeStatus(rag: number): "green" | "amber" | "red" {
  if (rag === RagStatus.Red) return "red";
  if (rag === RagStatus.Amber) return "amber";
  return "green";
}

export interface DashboardFlaggedStudentDto {
  studentId: string;
  name: string;
  companyName: string;
  rag: number; // RagStatus
  reason: string;
}

export interface SchoolDashboardDto {
  journalTodayPct: number;
  pendingApprovals: number;
  lateVisits: number;
  flagged: DashboardFlaggedStudentDto[];
}

export interface NotificationDto {
  id: string;
  type: string;
  payloadJson: string;
  isRead: boolean;
  createdAt: string;
}

export interface CommentDto {
  id: string;
  journalEntryId: string;
  teacherId: string;
  text: string;
  createdAt: string;
}

/** VOK-H4-E2 — respons GET /journals/for-teacher/{placementId} (lihat backend Dtos.cs utk alasan DTO baru ini, bukan JournalDto yang dipakai ulang). */
export interface JournalWithCommentsDto {
  entry: JournalDto;
  comments: CommentDto[];
}

export interface PlacementDto {
  id: string;
  studentId: string;
  companyId: string;
  periodId: string;
  teacherId: string;
  mentorUserId: string | null;
  status: number; // PlacementStatus: Active=0, Completed=1, Terminated=2
}

export interface StudentDto {
  id: string;
  fullName: string;
  nisn: string | null;
  majorId: string;
  classroom: string;
  userId: string | null;
}
