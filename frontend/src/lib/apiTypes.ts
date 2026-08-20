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

export interface SaStudentDto {
  id: string;
  tenantId: string;
  schoolName: string;
  fullName: string;
  nisn: string | null;
  majorName: string;
  classroom: string;
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

export interface MajorOptionDto { id: string; name: string; }

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

/**
 * VOK-H5-E1/E2 — kunjungan, rubrik, penilaian dua sisi, rekap+export, sertifikat. Sama konvensi
 * enum-sbg-angka spt di atas (lihat komentar besar di puncak file) — TIDAK ADA pengecualian.
 *
 * [CATATAN, bukan diperbaiki di sini]: `PeriodSummary.status` (di atas) DITULIS sbg union string
 * literal padahal `PeriodDto.Status` backend (Vokasia.Api/Endpoints/Dtos.cs) adalah `PeriodStatus`
 * enum — bakal datang sbg ANGKA sama seperti semua enum lain, bukan string "Active" dst. Field itu
 * TIDAK PERNAH dibandingkan/dipakai di mana pun sejauh ini (dikonfirmasi grep — cuma dirender jadi
 * label nama periode, bukan status-nya) jadi bug laten itu belum pernah termanifestasi; TIDAK
 * disentuh di sini (di luar cakupan H5-E2, bukan file yang tiket ini ubah) - kode BARU di bawah
 * sengaja pakai `PeriodStatusNum` terpisah (angka, benar) utk kebutuhan filter periode fase
 * Assessment di halaman nilai, supaya tidak mewarisi kesalahan yang sama.
 */
export const PeriodStatusNum = { Draft: 0, Active: 1, Assessment: 2, Closed: 3 } as const;

export const RubricAspectKind = { Teknis: 0, Softskill: 1, Kehadiran: 2 } as const;
export const ExportFormat = { Xlsx: 0, Pdf: 1 } as const;

export interface VisitDto {
  id: string;
  placementId: string;
  teacherId: string;
  date: string;
  notes: string;
  photoKey: string | null;
  signatureKey: string | null;
  createdAt: string;
}

export interface RubricAspectDto {
  id: string;
  name: string;
  kind: number; // RubricAspectKind
  weight: number;
}

export interface RubricDto {
  id: string;
  name: string;
  isDefault: boolean;
  aspects: RubricAspectDto[];
}

export interface AssessmentAspectDto {
  aspectId: string;
  aspectName: string;
  kind: number; // RubricAspectKind
  weight: number;
  mentorValue: number | null;
  teacherValue: number | null;
}

export interface AssessmentDto {
  id: string;
  placementId: string;
  aspects: AssessmentAspectDto[];
  mentorDone: boolean;
  teacherDone: boolean;
  finalScore: number | null;
  isFinal: boolean;
}

export interface IncompleteAssessmentDto {
  placementId: string;
  missingAspectNames: string[];
}

export interface FinalizeAssessmentResult {
  finalized: string[];
  incomplete: IncompleteAssessmentDto[];
}

export interface RecapRowDto {
  placementId: string;
  studentName: string;
  companyName: string;
  mentorAvg: number | null;
  teacherAvg: number | null;
  finalScore: number | null;
  status: "BelumDinilai" | "Draft" | "Final"; // dibangun server via ternary string literal (GetGradeRecap) - BUKAN enum, string asli.
}

export interface ExportAcceptedDto {
  exportId: string;
}

/** VOK-H5-E2 — GET /api/mentors/assessment-queue (gap ditambal, lihat DECISIONS.md D34). */
export interface MentorAssessmentPlacementDto {
  placementId: string;
  studentName: string;
  companyName: string;
  periodName: string;
}

export interface CertificateDownloadDto {
  downloadUrl: string;
}

export interface VerifyCertificateDto {
  studentName: string;
  schoolName: string;
  companyName: string;
  periodLabel: string;
  issuedAt: string;
  valid: boolean;
}

/**
 * VOK-H6-E1/E2 — /sa (tenants, DUDI, plans, ops), billing, portofolio. Sama konvensi enum-sbg-angka
 * (lihat komentar besar puncak file) — `InvoiceStatus`/`FeatureFlagKey` datang sbg ANGKA dari API.
 */
export const InvoiceStatus = { Issued: 0, ProofUploaded: 1, Paid: 2 } as const;
export const FeatureFlagKey = { GeotagAllowed: 0, ParentDigest: 1 } as const;

export interface TenantDto {
  id: string;
  schoolName: string;
  npsn: string | null;
  city: string | null;
  address: string | null;
  planId: string | null;
  isActive: boolean;
  createdAt: string;
}

export interface TenantStatsDto {
  studentCount: number;
  activePlacementCount: number;
  staffCount: number;
}

export interface TenantDetailDto {
  tenant: TenantDto;
  stats: TenantStatsDto;
}

export interface PlanDto {
  id: string;
  name: string;
  priceMonthly: number;
  maxStudents: number;
  maxPlacements: number;
}

/** VOK-H6-E1 §2 — registry DUDI global. `mergedIntoId` non-null = company ini sudah digabung ke company lain. */
export interface CompanyDto {
  id: string;
  name: string;
  sector: string | null;
  city: string | null;
  address: string | null;
  contactPerson: string | null;
  isVerified: boolean;
  mergedIntoId: string | null;
}

export interface CompanySearchDto {
  id: string;
  name: string;
  city: string | null;
}

export interface MergeResultDto {
  sourceId: string;
  targetId: string;
  movedTenantCompanies: number;
  movedPlacements: number;
}

export interface InvoiceDto {
  id: string;
  tenantId: string;
  periodMonth: string;
  amount: number;
  status: number; // InvoiceStatus
  proofKey: string | null;
}

export interface KpiDto {
  activeTenants: number;
  activeStudents: number;
  journalsToday: number;
  journalFillRate: number;
  mrr: number;
}

export interface HealthDto {
  queueDepth: number | null;
  dlqCount: number | null;
  failedJobs: number | null;
  outboxUnpublished: number;
  apiP95Ms: number | null;
  diskPct: number | null;
}

export interface AuditDto {
  id: string;
  tenantId: string | null;
  actorUserId: string;
  /** VOK-H6-E3: non-null = actorUserId beraksi SEBAGAI user ini (impersonasi SuperAdmin). */
  actingAsUserId: string | null;
  action: string;
  entity: string;
  entityId: string;
  metaJson: string;
  createdAt: string;
}

export interface PortfolioJournalSampleDto {
  journalEntryId: string;
  text: string;
  submittedAt: string;
}

export interface PortfolioCertificateDto {
  certCode: string;
  issuedAt: string;
}

export interface PortfolioDto {
  headline: string | null;
  verifiedCompetencies: string[];
  sampleJournals: PortfolioJournalSampleDto[];
  certificate: PortfolioCertificateDto | null;
  isPublished: boolean;
  slug: string | null;
}

export interface PublishPortfolioResult {
  slug: string;
}

// VOK-H6-E3 §2 — target picker StartImpersonation (GET /sa/tenants/{id}/staff, reuse SchoolUserDto).
export const UserRole = {
  SuperAdmin: 0,
  TenantAdmin: 1,
  DeptHead: 2,
  Teacher: 3,
  IndustryMentor: 4,
  Student: 5,
  ParentViewer: 6,
} as const;

export interface SchoolUserDto {
  id: string;
  email: string;
  fullName: string;
  role: number; // UserRole
  isActive: boolean;
}

/** VOK-H6-E1 §6 GetPublicPortfolio — TANPA NISN/kontak (server-side guaranteed, lihat backend PortfolioEndpoints.AssertPublicDtoHasNoSensitiveFields). */
export interface PublicPortfolioDto {
  studentName: string;
  schoolName: string;
  majorName: string;
  year: number;
  companyName: string;
  durationLabel: string;
  verifiedCompetencies: string[];
  sampleThumbnailUrls: string[];
  hasCertificate: boolean;
}
