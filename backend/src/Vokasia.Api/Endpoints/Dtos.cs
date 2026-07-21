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
