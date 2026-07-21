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
