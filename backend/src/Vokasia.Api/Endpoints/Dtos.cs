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
public record SaStudentDto(Guid Id, Guid TenantId, string SchoolName, string FullName, string? Nisn, string MajorName, string Classroom);
public record CreateStudentRequest(string FullName, string? Nisn, Guid MajorId, string Classroom);
public record UpdateStudentRequest(string FullName, string? Nisn, Guid MajorId, string Classroom);
public record ImportRowError(int Row, string Column, string Message);
public record ImportResultDto(int Imported, List<ImportRowError> Errors);

/// <summary>VOK-H3-E3 §2: bentuk satu baris CSV import siswa, DIVALIDASI via ImportStudentRowValidator per baris (bukan lewat ValidationFilter global — baris ini dikonstruksi manual di dalam handler, bukan argumen endpoint terikat framework).</summary>
public record ImportStudentRow(string FullName, string? Nisn, string MajorName, string Classroom);

public record CompanyDto(Guid Id, string Name, string? Sector, string? City, string? Address, string? ContactPerson, bool IsVerified, Guid? MergedIntoId);
public record ProposeCompanyRequest(string Name, string? Sector, string? City, string? Address, string? ContactPerson);

// VOK-H6-E1 §2: /sa/companies — registry DUDI global (FR-SA-02).
public record CreateCompanyRequest(string Name, string? Sector, string? City, string? Address, string? ContactPerson);
public record MergeCompaniesRequest(Guid SourceId, Guid TargetId);
public record MergeResultDto(Guid SourceId, Guid TargetId, int MovedTenantCompanies, int MovedPlacements);
public record CompanySearchDto(Guid Id, string Name, string? City);

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

// ExportFormat/ExportStatus pindah ke Vokasia.Domain.Common (dipakai jg oleh ExportRequest entity
// + Vokasia.Worker consumer, dua assembly terpisah dari Vokasia.Api - lihat Enums.cs).
// PeriodId TIDAK diulang di body - sudah ada di route (POST /api/periods/{periodId}/exports).
public record RequestExportRequest(ExportFormat Format);
public record ExportAcceptedDto(Guid ExportId);

// VOK-H5-E2 (FE) — gap ditambal, lihat DECISIONS.md D34: mentor (lintas-tenant, TenantId=null)
// TIDAK bisa panggil GET /api/periods (policy TenantMember butuh klaim tenant_id) utk cari periode
// fase Assessment sendiri, DAN ListPlacements (H2-E1) tak py filter mentorUserId sama sekali -
// dua gap sekaligus yang jadikan "daftar siswa fase Assessment" (AC literal mentor/nilai/page.tsx)
// mustahil dibangun dari endpoint yang ADA. Endpoint baru INI (bukan menambal 2 endpoint lama)
// dipilih krn paling konsisten dgn precedent GetPendingApprovals (JournalEndpoints) - "placements
// milikku" query MANDIRI, tanpa perlu periodId dari caller sama sekali (mentor lintas-tenant/
// lintas-periode by design).
public record MentorAssessmentPlacementDto(Guid PlacementId, string StudentName, string CompanyName, string PeriodName);

public record CertificateDownloadDto(string DownloadUrl);
public record VerifyCertificateDto(string StudentName, string SchoolName, string CompanyName, string PeriodLabel, DateTimeOffset IssuedAt, bool Valid);

// VOK-H6-E1 §1: /sa/tenants — wizard provisioning + CRUD.
public record CreateTenantRequest(string SchoolName, string? Npsn, string City, string AdminEmail, string AdminName, Guid PlanId);
public record UpdateTenantRequest(string SchoolName, string? Npsn, string? Address, string City, Guid? PlanId);
public record TenantDto(Guid Id, string SchoolName, string? Npsn, string? City, string? Address, Guid? PlanId, bool IsActive, DateTimeOffset CreatedAt);
public record TenantStatsDto(int StudentCount, int ActivePlacementCount, int StaffCount);
public record TenantDetailDto(TenantDto Tenant, TenantStatsDto Stats);
public record DeactivateTenantRequest(string Reason);

// VOK-H6-E1 §3: Plans (minimal CRUD sekarang — feature flags menyusul di slice terpisah).
public record PlanRequest(string Name, decimal PriceMonthly, int MaxStudents, int MaxPlacements);
public record PlanDto(Guid Id, string Name, decimal PriceMonthly, int MaxStudents, int MaxPlacements);

// VOK-H6-E1 §6: Portfolio publik siswa (FR-CRT-03).
public record PortfolioJournalSampleDto(Guid JournalEntryId, string Text, DateTimeOffset SubmittedAt);
public record PortfolioCertificateDto(string CertCode, DateTimeOffset IssuedAt);
public record PortfolioDto(string? Headline, List<string> VerifiedCompetencies, List<PortfolioJournalSampleDto> SampleJournals, PortfolioCertificateDto? Certificate, bool IsPublished, string? Slug);
public record UpdatePortfolioRequest(string? Headline, List<Guid> SampleJournalIds);
public record PublishPortfolioResult(string Slug);

/// <summary>NFR-SEC-05: TANPA NISN/kontak — ditegakkan STRUKTURAL (tak ada properti utk itu di sini) + assert reflection runtime di PublishPortfolio (lihat PortfolioEndpoints.AssertPublicDtoHasNoSensitiveFields).</summary>
public record PublicPortfolioDto(string StudentName, string SchoolName, string MajorName, int Year, string CompanyName, string DurationLabel, List<string> VerifiedCompetencies, List<string> SampleThumbnailUrls, bool HasCertificate);

// VOK-H6-E1 §5: Billing (FR-BIL-01..03).
public record InvoiceDto(Guid Id, Guid TenantId, DateOnly PeriodMonth, decimal Amount, InvoiceStatus Status, string? ProofKey);
public record UploadPaymentProofRequest(string ObjectKey);

// VOK-H6-E1 §3: Feature flags (FR-SA-03).
public record SetFeatureFlagRequest(FeatureFlagKey Key, bool Enabled);

// VOK-H6-E1 §4: Ops — KPI/health/audit (FR-SA-05..07).
public record KpiDto(int ActiveTenants, int ActiveStudents, int JournalsToday, double JournalFillRate, decimal Mrr);
public record HealthDto(int? QueueDepth, int? DlqCount, int? FailedJobs, int OutboxUnpublished, double? ApiP95Ms, double? DiskPct);
// ActingAsUserId (VOK-H6-E3): non-null = ActorUserId beraksi SEBAGAI user ini (impersonasi) — lihat
// AuditLog entity + VokasiaDbContext.SaveChangesAsync utk mekanisme pengisiannya.
public record AuditDto(Guid Id, Guid? TenantId, Guid ActorUserId, Guid? ActingAsUserId, string Action, string Entity, string EntityId, string MetaJson, DateTimeOffset CreatedAt);
