using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vokasia.Api.Auth;
using Vokasia.Api.Authorization;
using Vokasia.Api.Validation;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Persistence;
using Vokasia.Infrastructure.Queries;

namespace Vokasia.Api.Endpoints;

public sealed record TeacherMonitoringCreateRequest(
    string? Status,
    string? Note,
    string? Visibility,
    Guid? FollowUpVisitId,
    string? FollowUpContext);

public sealed record TeacherMonitoringEventDto(
    Guid Id,
    Guid PlacementId,
    string Status,
    string? Note,
    string Visibility,
    Guid? FollowUpVisitId,
    string? FollowUpContext,
    DateTimeOffset CreatedAt);

public sealed record TeacherMonitoringOverdueFindingDto(
    Guid PlacementId,
    string StudentName,
    string CompanyName,
    string Stage,
    string DueDate,
    string Label);

public sealed record TeacherMonitoringWorkspaceDto(
    List<TeacherMonitoringPlacementDto> Placements,
    List<TeacherMonitoringEventDto> Events,
    List<TeacherMonitoringOverdueFindingDto> OverdueFindings);

public sealed record TeacherMonitoringPlacementDto(Guid PlacementId, string StudentName, string CompanyName);

public sealed record TeacherLearningRecordEvidenceDto(Guid JournalEntryId, string Text, DateTimeOffset SubmittedAt);

public sealed record TeacherLearningRecordCriterionDto(
    Guid CriterionSnapshotId,
    string Name,
    string Description,
    int SortOrder,
    int? Score,
    string? Comment,
    List<TeacherLearningRecordEvidenceDto> Evidence);

public sealed record TeacherLearningRecordStageDto(
    string Stage,
    string Status,
    string OperationalState,
    string OperationalStateLabel,
    string? EvaluatorDisplayName,
    Guid? RevisionId,
    DateTimeOffset? FinalizedAt,
    string? OverallNote,
    List<TeacherLearningRecordCriterionDto> Criteria);

public sealed record TeacherLearningRecordPlacementDto(
    Guid PlacementId,
    string StudentName,
    string CompanyName,
    string PeriodName,
    DateOnly StartDate,
    DateOnly EndDate,
    List<TeacherLearningRecordStageDto> Stages);

/// <summary>
/// Private Teacher monitoring surface. Statuses are manual, events are append-only, and overdue
/// assessments are read-only operational findings; this endpoint never mutates Learning Record scores.
/// </summary>
public static class TeacherMonitoringEndpoints
{
    public static IEndpointRouteBuilder MapTeacherMonitoringEndpoints(this IEndpointRouteBuilder app)
    {
        var workspace = app.MapGroup("/api/teacher/learning-record/monitoring")
            .WithTags("Learning Record")
            .RequireAuthorization(RbacPolicies.TeacherPlus);
        workspace.MapGet("", ListWorkspace);

        var placement = app.MapGroup("/api/placements/{placementId:guid}/teacher-monitoring")
            .WithTags("Learning Record")
            .RequireAuthorization(RbacPolicies.TeacherPlus);
        placement.MapGet("", ListPlacement);
        placement.MapPost("", CreateEvent);

        var detail = app.MapGroup("/api/placements/{placementId:guid}/teacher-learning-record")
            .WithTags("Learning Record")
            .RequireAuthorization(RbacPolicies.TeacherPlus);
        detail.MapGet("", GetLearningRecordDetail);

        return app;
    }

    private static async Task<IResult> ListWorkspace(VokasiaDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        var placementIds = await ScopedPlacements(db, tenant)
            .Select(item => item.Id)
            .ToListAsync(ct);
        return Results.Ok(await BuildWorkspaceAsync(db, placementIds, ct));
    }

    private static async Task<IResult> ListPlacement(
        Guid placementId,
        VokasiaDbContext db,
        ITenantContext tenant,
        CancellationToken ct)
    {
        var exists = await ScopedPlacements(db, tenant).AnyAsync(item => item.Id == placementId, ct);
        if (!exists)
        {
            return Results.NotFound();
        }

        return Results.Ok(await BuildWorkspaceAsync(db, [placementId], ct));
    }

    private static async Task<IResult> GetLearningRecordDetail(
        Guid placementId,
        VokasiaDbContext db,
        ITenantContext tenant,
        LearningRecordQueryService queries,
        CancellationToken ct)
    {
        var placement = await (
            from candidate in ScopedPlacements(db, tenant)
            join period in db.Periods.AsNoTracking() on candidate.PeriodId equals period.Id
            join student in db.Students.AsNoTracking() on candidate.StudentId equals student.Id
            join company in db.Companies.AsNoTracking() on candidate.CompanyId equals company.Id
            where candidate.Id == placementId
            select new
            {
                candidate.Id,
                student.FullName,
                CompanyName = company.Name,
                PeriodName = period.Name,
                period.StartDate,
                period.EndDate,
            }).SingleOrDefaultAsync(ct);
        if (placement is null)
        {
            return Results.NotFound();
        }

        var stages = new List<TeacherLearningRecordStageDto>(2);
        foreach (var stage in new[] { LearningAssessmentStage.Middle, LearningAssessmentStage.Final })
        {
            var projection = await queries.GetStageAsync(placement.Id, stage, AppTimeZone.TodayJakarta(), ct);
            stages.Add(ToTeacherStageDto(projection, stage, placement.StartDate, placement.EndDate));
        }

        return Results.Ok(new TeacherLearningRecordPlacementDto(
            placement.Id, placement.FullName, placement.CompanyName, placement.PeriodName,
            placement.StartDate, placement.EndDate, stages));
    }

    private static async Task<IResult> CreateEvent(
        Guid placementId,
        TeacherMonitoringCreateRequest request,
        VokasiaDbContext db,
        ITenantContext tenant,
        CancellationToken ct)
    {
        if (!tenant.UserId.HasValue || !tenant.TenantId.HasValue)
        {
            return Results.Forbid();
        }

        var placement = await ScopedPlacements(db, tenant).SingleOrDefaultAsync(item => item.Id == placementId, ct);
        if (placement is null)
        {
            return Results.NotFound();
        }

        if (!TeacherMonitoringValidators.TryParseStatus(request.Status, out var status) ||
            !TeacherMonitoringValidators.TryParseVisibility(request.Visibility, out var visibility))
        {
            return Results.UnprocessableEntity(new { message = "Status atau visibilitas monitoring tidak dikenal." });
        }

        var validationError = TeacherMonitoringValidators.Validate(status, request.Note, request.FollowUpContext);
        if (validationError is not null)
        {
            return Results.UnprocessableEntity(new { message = validationError });
        }

        if (request.FollowUpVisitId.HasValue && !await db.Visits.AnyAsync(item =>
                item.Id == request.FollowUpVisitId.Value && item.TenantId == placement.TenantId && item.PlacementId == placement.Id, ct))
        {
            return Results.UnprocessableEntity(new { message = "Kunjungan tindak lanjut tidak berasal dari placement ini." });
        }

        var item = new TeacherMonitoringEvent
        {
            Id = Guid.NewGuid(), TenantId = placement.TenantId, PlacementId = placement.Id,
            TeacherUserId = tenant.UserId.Value, Status = status, Note = request.Note?.Trim(),
            Visibility = visibility, FollowUpVisitId = request.FollowUpVisitId,
            FollowUpContext = request.FollowUpContext?.Trim(), CreatedAt = DateTimeOffset.UtcNow,
        };
        db.TeacherMonitoringEvents.Add(item);
        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(), TenantId = placement.TenantId, ActorUserId = tenant.UserId.Value,
            Action = "TeacherMonitoringEventCreated", Entity = nameof(TeacherMonitoringEvent), EntityId = item.Id.ToString(),
            MetaJson = JsonSerializer.Serialize(new { item.PlacementId, item.Status, item.Visibility }),
        });
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/placements/{placement.Id}/teacher-monitoring/{item.Id}", ToDto(item));
    }

    private static IQueryable<Placement> ScopedPlacements(VokasiaDbContext db, ITenantContext tenant)
    {
        if (!tenant.TenantId.HasValue || !tenant.UserId.HasValue)
        {
            return db.Placements.Where(_ => false);
        }

        return tenant.Role == nameof(UserRole.Teacher)
            ? db.Placements.Where(item => item.TenantId == tenant.TenantId.Value && item.TeacherId == tenant.UserId.Value)
            : db.Placements.Where(item => item.TenantId == tenant.TenantId.Value);
    }

    private static async Task<TeacherMonitoringWorkspaceDto> BuildWorkspaceAsync(
        VokasiaDbContext db,
        IReadOnlyCollection<Guid> placementIds,
        CancellationToken ct)
    {
        if (placementIds.Count == 0)
        {
            return new([], [], []);
        }

        var placements = await (
            from placement in db.Placements.AsNoTracking()
            join student in db.Students.AsNoTracking() on placement.StudentId equals student.Id
            join company in db.Companies.AsNoTracking() on placement.CompanyId equals company.Id
            where placementIds.Contains(placement.Id)
            orderby student.FullName, company.Name
            select new TeacherMonitoringPlacementDto(placement.Id, student.FullName, company.Name))
            .ToListAsync(ct);

        var events = await db.TeacherMonitoringEvents.AsNoTracking()
            .Where(item => placementIds.Contains(item.PlacementId))
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new TeacherMonitoringEventDto(
                item.Id, item.PlacementId, item.Status.ToString(), item.Note, item.Visibility.ToString(),
                item.FollowUpVisitId, item.FollowUpContext, item.CreatedAt))
            .ToListAsync(ct);

        var placementContexts = await (
            from placement in db.Placements.AsNoTracking()
            join period in db.Periods.AsNoTracking() on placement.PeriodId equals period.Id
            join student in db.Students.AsNoTracking() on placement.StudentId equals student.Id
            join company in db.Companies.AsNoTracking() on placement.CompanyId equals company.Id
            where placementIds.Contains(placement.Id)
            select new { PlacementId = placement.Id, student.FullName, CompanyName = company.Name, period.StartDate, period.EndDate })
            .ToListAsync(ct);

        var assessmentStatuses = await db.LearningAssessments.AsNoTracking()
            .Where(item => placementIds.Contains(item.PlacementId))
            .Select(item => new { item.PlacementId, item.Stage, item.Status })
            .ToListAsync(ct);
        var statusByStage = assessmentStatuses.ToDictionary(item => (item.PlacementId, item.Stage), item => item.Status);
        var today = AppTimeZone.TodayJakarta();
        var overdue = placementContexts
            .SelectMany(placement => Enum.GetValues<LearningAssessmentStage>().Select(stage =>
            {
                var status = statusByStage.TryGetValue((placement.PlacementId, stage), out var existingStatus)
                    ? existingStatus
                    : LearningAssessmentStatus.Draft;
                var state = LearningRecordRules.GetOperationalState(stage, status, placement.StartDate, placement.EndDate, today);
                return new { placement, stage, due = LearningRecordRules.GetDueDate(stage, placement.StartDate, placement.EndDate), state };
            }))
            .Where(item => item.state == LearningAssessmentOperationalState.Overdue)
            .Select(item => new TeacherMonitoringOverdueFindingDto(
                item.placement.PlacementId, item.placement.FullName, item.placement.CompanyName, item.stage.ToString(),
                item.due.ToString("yyyy-MM-dd"), LearningRecordRules.GetOperationalStateLabel(item.state)))
            .ToList();

        return new(placements, events, overdue);
    }

    private static TeacherMonitoringEventDto ToDto(TeacherMonitoringEvent item) => new(
        item.Id, item.PlacementId, item.Status.ToString(), item.Note, item.Visibility.ToString(),
        item.FollowUpVisitId, item.FollowUpContext, item.CreatedAt);

    private static TeacherLearningRecordStageDto ToTeacherStageDto(
        LearningAssessmentStageProjection? projection,
        LearningAssessmentStage stage,
        DateOnly startDate,
        DateOnly endDate)
    {
        var assessment = projection?.Assessment;
        var status = assessment?.Status ?? LearningAssessmentStatus.Draft;
        var state = LearningRecordRules.GetOperationalState(stage, status, startDate, endDate, AppTimeZone.TodayJakarta());
        var showRevision = status == LearningAssessmentStatus.Finalized && projection?.LatestRevision is not null;
        var draftByCriterion = projection?.DraftCriteria.ToDictionary(item => item.CriterionSnapshotId) ?? [];
        var revisionByCriterion = projection?.Result.RevisionCriteria.ToDictionary(item => item.CriterionSnapshotId) ?? [];
        var evidenceByCriterion = projection?.Result.Evidence
            .GroupBy(item => item.CriterionSnapshotId)
            .ToDictionary(group => group.Key, group => group
                .Select(item => new TeacherLearningRecordEvidenceDto(item.JournalEntryId, item.Text, item.SubmittedAt))
                .ToList()) ?? [];
        var criteria = projection?.Snapshot?.Criteria.OrderBy(item => item.SortOrder).Select(criterion =>
        {
            var revisionCriterion = revisionByCriterion.GetValueOrDefault(criterion.Id);
            var draftCriterion = draftByCriterion.GetValueOrDefault(criterion.Id);
            return new TeacherLearningRecordCriterionDto(
                criterion.Id, criterion.Name, criterion.Description, criterion.SortOrder,
                showRevision ? revisionCriterion?.Score : draftCriterion?.Score,
                showRevision ? revisionCriterion?.Comment : draftCriterion?.Comment,
                evidenceByCriterion.GetValueOrDefault(criterion.Id) ?? []);
        }).ToList() ?? [];

        return new TeacherLearningRecordStageDto(
            stage.ToString(), status.ToString(), state.ToString(), LearningRecordRules.GetOperationalStateLabel(state),
            showRevision ? projection!.LatestRevision!.EvaluatorDisplayName : null,
            showRevision ? projection!.LatestRevision!.Id : null,
            showRevision ? projection!.LatestRevision!.FinalizedAt : null,
            showRevision ? projection!.LatestRevision!.OverallNote : assessment?.OverallNote,
            criteria);
    }
}
