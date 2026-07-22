using Vokasia.Domain.Common;

namespace Vokasia.Api.Endpoints;

// DTO bersama H2-E1 (VOK-H2-E1). [ASSUMPTION]: belum ada file OpenAPI beku terpisah di repo saat
// ticket ini dikerjakan — bentuk request/response mengikuti persis signature di ticket/VOK-H2-E1.md.

public record Paged<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public record PeriodDto(Guid Id, string Name, DateOnly StartDate, DateOnly EndDate, string ClassLevels, PeriodStatus Status);
public record CreatePeriodRequest(string Name, DateOnly StartDate, DateOnly EndDate, string[] ClassLevels, List<HolidayDto>? Holidays);
public record UpdatePeriodRequest(string Name, DateOnly StartDate, DateOnly EndDate, string[] ClassLevels);
public record HolidayDto(DateOnly Date, string Label);

public record StudentDto(Guid Id, string FullName, string? Nisn, Guid MajorId, string Classroom, Guid? UserId);
public record CreateStudentRequest(string FullName, string? Nisn, Guid MajorId, string Classroom);
public record UpdateStudentRequest(string FullName, string? Nisn, Guid MajorId, string Classroom);
public record ImportRowError(int Row, string Column, string Message);
public record ImportResultDto(int Imported, List<ImportRowError> Errors);

/// <summary>VOK-H3-E3 §2: bentuk satu baris CSV import siswa, DIVALIDASI via ImportStudentRowValidator per baris (bukan lewat ValidationFilter global — baris ini dikonstruksi manual di dalam handler, bukan argumen endpoint terikat framework).</summary>
public record ImportStudentRow(string FullName, string? Nisn, string MajorName, string Classroom);

public record CompanyDto(Guid Id, string Name, string? Sector, string? City, string? Address, string? ContactPerson, bool IsVerified);
public record ProposeCompanyRequest(string Name, string? Sector, string? City, string? Address, string? ContactPerson);

public record CreatePlacementRequest(Guid StudentId, Guid CompanyId, Guid PeriodId, Guid TeacherId, string? MentorEmail);
public record PlacementDto(Guid Id, Guid StudentId, Guid CompanyId, Guid PeriodId, Guid TeacherId, Guid? MentorUserId, PlacementStatus Status);
public record BulkResult(List<Guid> SuccessIds, List<ImportRowError> Errors);

public record InviteUserRequest(string Email, string FullName, UserRole Role);
public record SchoolUserDto(Guid Id, string Email, string FullName, UserRole Role, bool IsActive);

// VOK-H3-E1: jurnal siswa/mentor/guru.
public record CompetencyDto(Guid Id, string Name, Guid MajorId);

public record JournalSlotDto(Guid Id, DateOnly Date, JournalSlotStatus Status);
public record WeekDayStatusDto(DateOnly Date, JournalSlotStatus Status);
public record PhotoDto(Guid Id, string ObjectKey, string? ThumbKey, PhotoStatus Status);

public record JournalDto(
    Guid Id, Guid SlotId, Guid PlacementId, string Text, JournalEntryStatus Status,
    string? MentorNote, DateTimeOffset SubmittedAt, DateTimeOffset? ApprovedAt,
    List<PhotoDto> Photos, List<Guid> CompetencyIds);

public record TodayJournalDto(
    JournalSlotDto Slot, JournalDto? Entry, List<CompetencyDto> Competencies,
    List<WeekDayStatusDto> WeekStatus, int Streak);

public record SubmitJournalRequest(Guid SlotId, string Text, List<Guid> CompetencyIds, List<Guid>? PhotoIds);

public record UploadRequest(string FileName, string ContentType, long SizeBytes);
public record PresignedUploadDto(string UploadUrl, string ObjectKey, int ExpiresIn);
public record AttachPhotoRequest(string ObjectKey);

public record JournalFilter(Guid? PlacementId, JournalEntryStatus? Status, DateOnly? From, DateOnly? To);

public record PendingGroupDto(Guid StudentId, string StudentFullName, List<JournalDto> Entries);

public record ApproveJournalRequest(string? Note);
public record RejectJournalRequest(string Reason);
public record BatchApproveRequest(List<Guid> Ids);
public record BatchFailure(Guid Id, string Reason);
public record BatchResult(List<Guid> Approved, List<BatchFailure> Failed);

public record AddCommentRequest(string Text);
public record CommentDto(Guid Id, Guid JournalEntryId, Guid TeacherId, string Text, DateTimeOffset CreatedAt);

// VOK-H4-E1 §4: notifikasi in-app + dashboard sekolah.
public record NotificationDto(Guid Id, string Type, string PayloadJson, bool IsRead, DateTimeOffset CreatedAt);

public record DashboardFlaggedStudentDto(Guid StudentId, string Name, string CompanyName, RagStatus Rag, string Reason);

public record SchoolDashboardDto(double JournalTodayPct, int PendingApprovals, int LateVisits, List<DashboardFlaggedStudentDto> Flagged);

/// <summary>
/// VOK-H4-E2 §2 (halaman guru bimbingan) — GAP ditemukan (dicatat DECISIONS.md, bukan diam-diam):
/// `ListJournals` yang sudah ada (H3-E1) mengunci diri ke `RbacPolicies.StudentSelf` DAN internal
/// handler-nya look-up `Students.FirstOrDefault(s => s.UserId == caller)` — TIDAK BISA dipakai
/// guru sama sekali (caller bukan siswa -> selalu Forbid, bukan soal query-filter). Tak ada pula
/// endpoint utk baca `TeacherComment` (`AddTeacherComment` cuma tulis, tak pernah ada `ListComments`).
/// `JournalWithCommentsDto` DTO baru (BUKAN ubah `JournalDto` yang sudah dipakai banyak endpoint
/// lain sbg positional record - menambah field di situ akan pecah semua call-site `new
/// JournalDto(...)` yang sudah ada) - dipasang di endpoint BARU `GET /api/journals/for-teacher/{placementId}`.
/// </summary>
public record JournalWithCommentsDto(JournalDto Entry, List<CommentDto> Comments);

// VOK-H5-E1: kunjungan guru, rubrik, penilaian dua sisi, rekap+export, sertifikat.
public record VisitDto(Guid Id, Guid PlacementId, Guid TeacherId, DateOnly Date, string Notes, string? PhotoKey, string? SignatureKey, DateTimeOffset CreatedAt);
public record CreateVisitRequest(DateOnly Date, string Notes, string? PhotoKey, string? SignatureDataUrl);

public record RubricAspectInput(string Name, RubricAspectKind Kind, int Weight);
public record RubricAspectDto(Guid Id, string Name, RubricAspectKind Kind, int Weight);
public record CreateRubricRequest(string Name, List<RubricAspectInput> Aspects);
public record UpdateRubricRequest(string Name, List<RubricAspectInput> Aspects);
public record RubricDto(Guid Id, string Name, bool IsDefault, List<RubricAspectDto> Aspects);

public record ScoreInput(Guid AspectId, decimal Value);
public record AssessmentAspectDto(Guid AspectId, string AspectName, RubricAspectKind Kind, int Weight, decimal? MentorValue, decimal? TeacherValue);
public record AssessmentDto(Guid Id, Guid PlacementId, List<AssessmentAspectDto> Aspects, bool MentorDone, bool TeacherDone, decimal? FinalScore, bool IsFinal);

public record FinalizeAssessmentRequest(Guid PeriodId, Guid? PlacementId);
public record IncompleteAssessmentDto(Guid PlacementId, List<string> MissingAspectNames);
public record FinalizeAssessmentResult(List<Guid> Finalized, List<IncompleteAssessmentDto> Incomplete);

public record RecapRowDto(Guid PlacementId, string StudentName, string CompanyName, decimal? MentorAvg, decimal? TeacherAvg, decimal? FinalScore, string Status);

public enum ExportFormat { Xlsx, Pdf }
public record RequestExportRequest(Guid PeriodId, ExportFormat Format);
public record ExportAcceptedDto(Guid ExportId);

public record CertificateDownloadDto(string DownloadUrl);
public record VerifyCertificateDto(string StudentName, string SchoolName, string CompanyName, string PeriodLabel, DateTimeOffset IssuedAt, bool Valid);
