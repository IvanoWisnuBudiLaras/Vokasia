using Microsoft.EntityFrameworkCore;
using Vokasia.Api.Auth;
using Vokasia.Api.Authorization;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Api.Endpoints;

public sealed record StudentLearningRecordPlacementDto(
    Guid PlacementId,
    string CompanyName,
    string PeriodName,
    DateOnly StartDate,
    DateOnly EndDate,
    string ProgressState,
    string? CurrentStage,
    List<StudentLearningRecordStageDto> Stages,
    List<StudentLearningRecordMonitoringEventDto> MonitoringTimeline,
    StudentLegacyFinalAssessmentDto? LegacyFinalAssessment);

public sealed record StudentLearningRecordPlacementSummaryDto(
    Guid PlacementId,
    string CompanyName,
    string PeriodName,
    DateOnly StartDate,
    DateOnly EndDate,
    string ProgressState,
    string? CurrentStage,
    bool LegacyFinalOnly);

public sealed record StudentLearningRecordStageDto(
    string Stage,
    string EvaluatorDisplayName,
    DateTimeOffset FinalizedAt,
    string OverallNote,
    List<StudentLearningRecordCriterionDto> Criteria);

public sealed record StudentLearningRecordMonitoringEventDto(
    Guid Id,
    string Status,
    string? Note,
    string? FollowUpContext,
    DateTimeOffset CreatedAt);

public sealed record StudentLegacyFinalAssessmentDto(
    Guid AssessmentId,
    decimal? FinalScore,
    DateTimeOffset? FinalizedAt);

public sealed record StudentLearningRecordCriterionDto(
    Guid CriterionSnapshotId,
    string Name,
    string Description,
    int SortOrder,
    int Score,
    string? Comment,
    List<StudentLearningRecordEvidenceDto> Evidence);

public sealed record StudentLearningRecordEvidenceDto(
    Guid JournalEntryId,
    string Text,
    DateTimeOffset SubmittedAt);

/// <summary>
/// Private V3 Student Learning Record reads. This projection is deliberately separate from the
/// V2 weighted assessment routes and only exposes immutable finalized observations owned by the
/// authenticated student's placements.
/// </summary>
public static class LearningRecordReadEndpoints
{
    public static IEndpointRouteBuilder MapLearningRecordReadEndpoints(this IEndpointRouteBuilder app)
    {
        var records = app.MapGroup("/api/students/me/learning-records")
            .WithTags("Learning Record")
            .RequireAuthorization(RbacPolicies.StudentSelf);
        records.MapGet("", ListMine);
        records.MapGet("/{placementId:guid}", GetMine);
        return app;
    }

    private static async Task<IResult> ListMine(VokasiaDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        var student = await FindStudentAsync(db, tenant, ct);
        if (student is null)
        {
            return Results.NotFound();
        }

        var placements = await LoadPlacementsAsync(db, student.Id, ct);
        return Results.Ok(await BuildSummariesAsync(db, placements, ct));
    }

    private static async Task<IResult> GetMine(
        Guid placementId,
        VokasiaDbContext db,
        ITenantContext tenant,
        CancellationToken ct)
    {
        var student = await FindStudentAsync(db, tenant, ct);
        if (student is null)
        {
            return Results.NotFound();
        }

        var placement = await LoadPlacementsAsync(db, student.Id, ct, placementId);
        if (placement.Count == 0)
        {
            return Results.NotFound();
        }

        var record = (await BuildRecordsAsync(db, placement, ct)).Single();
        return Results.Ok(record);
    }

    private static Task<Student?> FindStudentAsync(VokasiaDbContext db, ITenantContext tenant, CancellationToken ct) =>
        !tenant.UserId.HasValue
            ? Task.FromResult<Student?>(null)
            : db.Students.AsNoTracking().SingleOrDefaultAsync(item => item.UserId == tenant.UserId.Value, ct);

    private static async Task<List<StudentPlacementProjection>> LoadPlacementsAsync(
        VokasiaDbContext db,
        Guid studentId,
        CancellationToken ct,
        Guid? placementId = null)
    {
        var query =
            from placement in db.Placements.AsNoTracking()
            join period in db.Periods.AsNoTracking() on placement.PeriodId equals period.Id
            join company in db.Companies.AsNoTracking() on placement.CompanyId equals company.Id
            join snapshotCandidate in db.PlacementLearningRecordSnapshots.AsNoTracking()
                on placement.Id equals snapshotCandidate.PlacementId into snapshotGroup
            from snapshot in snapshotGroup.DefaultIfEmpty()
            where placement.StudentId == studentId && (!placementId.HasValue || placement.Id == placementId.Value)
            orderby placement.Status == PlacementStatus.Active ? 0 : 1, period.EndDate descending, placement.CreatedAt descending, company.Name
            select new StudentPlacementProjection(
                placement.Id, placement.TenantId, placement.CompanyId, placement.PeriodId,
                snapshot == null ? company.Name : snapshot.CompanyDisplayName ?? company.Name,
                snapshot == null ? period.Name : snapshot.PeriodDisplayName ?? period.Name,
                snapshot == null || !snapshot.PeriodStartDate.HasValue ? period.StartDate : snapshot.PeriodStartDate.Value,
                snapshot == null || !snapshot.PeriodEndDate.HasValue ? period.EndDate : snapshot.PeriodEndDate.Value);

        return await query.ToListAsync(ct);
    }

    private static async Task<List<StudentLearningRecordPlacementSummaryDto>> BuildSummariesAsync(
        VokasiaDbContext db,
        IReadOnlyList<StudentPlacementProjection> placements,
        CancellationToken ct)
    {
        if (placements.Count == 0)
        {
            return [];
        }

        var placementIds = placements.Select(item => item.PlacementId).ToArray();
        var finalizedStages = await db.LearningAssessments.AsNoTracking()
            .Where(item => placementIds.Contains(item.PlacementId) && item.LatestFinalizedRevisionId.HasValue)
            .Select(item => new { item.PlacementId, item.Stage, item.Status })
            .ToListAsync(ct);
        var legacyFinalPlacementIds = (await db.Assessments.AsNoTracking()
            .Where(item => placementIds.Contains(item.PlacementId) && item.IsFinal)
            .Select(item => item.PlacementId)
            .ToListAsync(ct))
            .ToHashSet();
        var stagesByPlacement = finalizedStages
            .GroupBy(item => item.PlacementId)
            .ToDictionary(group => group.Key, group => group.ToList());

        return placements.Select(placement =>
        {
            var stages = stagesByPlacement.GetValueOrDefault(placement.PlacementId, []);
            var currentStage = stages.Any(item => item.Stage == LearningAssessmentStage.Final)
                ? nameof(LearningAssessmentStage.Final)
                : stages.Any(item => item.Stage == LearningAssessmentStage.Middle) ? nameof(LearningAssessmentStage.Middle) : null;
            var progressState = stages.Any(item => item.Status == LearningAssessmentStatus.Reopened)
                ? "CorrectionInProgress"
                : currentStage is null
                ? "AwaitingMiddle"
                : currentStage == nameof(LearningAssessmentStage.Final) ? "FinalComplete" : "MiddleComplete";
            return new StudentLearningRecordPlacementSummaryDto(
                placement.PlacementId, placement.CompanyName, placement.PeriodName, placement.StartDate, placement.EndDate,
                progressState, currentStage, legacyFinalPlacementIds.Contains(placement.PlacementId));
        }).ToList();
    }

    private static async Task<List<StudentLearningRecordPlacementDto>> BuildRecordsAsync(
        VokasiaDbContext db,
        IReadOnlyList<StudentPlacementProjection> placements,
        CancellationToken ct)
    {
        if (placements.Count == 0)
        {
            return [];
        }

        var placementIds = placements.Select(item => item.PlacementId).ToArray();
        var assessments = await db.LearningAssessments.AsNoTracking()
            .Where(item => placementIds.Contains(item.PlacementId) && item.LatestFinalizedRevisionId.HasValue)
            .Select(item => new StudentAssessmentProjection(
                item.Id, item.PlacementId, item.Stage, item.Status, item.LatestFinalizedRevisionId!.Value))
            .ToListAsync(ct);
        var revisionIds = assessments.Select(item => item.RevisionId).ToArray();
        var revisions = revisionIds.Length == 0
            ? []
            : await db.LearningAssessmentRevisions.AsNoTracking()
                .Where(item => revisionIds.Contains(item.Id))
                .ToListAsync(ct);
        var revisionCriteria = revisionIds.Length == 0
            ? []
            : await db.LearningAssessmentRevisionCriteria.AsNoTracking()
                .Where(item => revisionIds.Contains(item.RevisionId))
                .ToListAsync(ct);
        var snapshotIds = await db.PlacementLearningRecordSnapshots.AsNoTracking()
            .Where(item => placementIds.Contains(item.PlacementId))
            .Select(item => item.Id)
            .Distinct()
            .ToArrayAsync(ct);
        var snapshots = snapshotIds.Length == 0
            ? []
            : await db.PlacementLearningRecordCriterionSnapshots.AsNoTracking()
                .Where(item => snapshotIds.Contains(item.SnapshotId))
                .ToListAsync(ct);
        var assessmentIds = assessments.Select(item => item.AssessmentId).ToArray();
        var legacyFinalAssessments = await db.Assessments.AsNoTracking()
            .Where(item => placementIds.Contains(item.PlacementId) && item.IsFinal)
            .Select(item => new StudentLegacyFinalAssessmentProjection(
                item.PlacementId, item.Id, item.FinalScore, item.FinalizedAt))
            .ToListAsync(ct);
        var evidence = assessmentIds.Length == 0
            ? []
            : await (
                from assessment in db.LearningAssessments.AsNoTracking()
                join revision in db.LearningAssessmentRevisions.AsNoTracking() on assessment.LatestFinalizedRevisionId equals revision.Id
                join revisionCriterion in db.LearningAssessmentRevisionCriteria.AsNoTracking() on revision.Id equals revisionCriterion.RevisionId
                join revisionEvidence in db.LearningAssessmentRevisionCriterionEvidence.AsNoTracking() on revisionCriterion.Id equals revisionEvidence.RevisionCriterionId
                where assessmentIds.Contains(assessment.Id)
                    && assessment.Status == LearningAssessmentStatus.Finalized
                    && revision.TenantId == assessment.TenantId
                select new StudentEvidenceProjection(
                    assessment.Id, revisionCriterion.CriterionSnapshotId, revisionEvidence.JournalEntryId, revisionEvidence.Text, revisionEvidence.SubmittedAt))
                .ToListAsync(ct);

        var monitoringEvents = await (
                from monitoring in db.TeacherMonitoringEvents.AsNoTracking()
                join placement in db.Placements.AsNoTracking() on monitoring.PlacementId equals placement.Id
                where placementIds.Contains(monitoring.PlacementId)
                    && monitoring.Visibility == LearningRecordMonitoringVisibility.StudentVisible
                    && monitoring.TenantId == placement.TenantId
                orderby monitoring.CreatedAt descending, monitoring.Id descending
                select new StudentMonitoringProjection(
                    monitoring.PlacementId, monitoring.Id, monitoring.Status, monitoring.Note,
                    monitoring.FollowUpContext, monitoring.CreatedAt))
            .ToListAsync(ct);

        var assessmentsByRevision = assessments.ToDictionary(item => item.RevisionId);
        var revisionsById = revisions.ToDictionary(item => item.Id);
        var criteriaByRevision = revisionCriteria.GroupBy(item => item.RevisionId).ToDictionary(group => group.Key, group => group.ToList());
        var snapshotsById = snapshots.ToDictionary(item => item.Id);
        var evidenceByAssessmentAndCriterion = evidence
            .GroupBy(item => (item.AssessmentId, item.CriterionSnapshotId))
            .ToDictionary(group => group.Key, group => group
                .Select(item => new StudentLearningRecordEvidenceDto(item.JournalEntryId, item.Text, item.SubmittedAt))
                .ToList());
        var monitoringByPlacement = monitoringEvents
            .GroupBy(item => item.PlacementId)
            .ToDictionary(group => group.Key, group => group
                .Select(item => new StudentLearningRecordMonitoringEventDto(
                    item.Id, item.Status.ToString(), item.Note, item.FollowUpContext, item.CreatedAt))
                .ToList());
        var legacyByPlacement = legacyFinalAssessments
            .GroupBy(item => item.PlacementId)
            .ToDictionary(group => group.Key, group => group
                .OrderByDescending(item => item.FinalizedAt)
                .Select(item => new StudentLegacyFinalAssessmentDto(item.AssessmentId, item.FinalScore, item.FinalizedAt))
                .First());

        var records = new List<StudentLearningRecordPlacementDto>(placements.Count);
        foreach (var placement in placements)
        {
            var placementAssessments = assessments
                .Where(item => item.PlacementId == placement.PlacementId && revisionsById.ContainsKey(item.RevisionId))
                .ToList();
            var stages = placementAssessments
                .Select(item =>
                {
                    var revision = revisionsById[item.RevisionId];
                    var criteria = criteriaByRevision.GetValueOrDefault(revision.Id, [])
                        .Where(item => snapshotsById.ContainsKey(item.CriterionSnapshotId))
                        .OrderBy(item => snapshotsById[item.CriterionSnapshotId].SortOrder)
                        .Select(item =>
                        {
                            var snapshot = snapshotsById[item.CriterionSnapshotId];
                            return new StudentLearningRecordCriterionDto(
                                item.CriterionSnapshotId, snapshot.Name, snapshot.Description, snapshot.SortOrder,
                                item.Score, item.Comment,
                                evidenceByAssessmentAndCriterion.GetValueOrDefault((assessmentsByRevision[revision.Id].AssessmentId, item.CriterionSnapshotId), []));
                        })
                        .ToList();
                    return new StudentLearningRecordStageDto(
                        revision.Stage.ToString(), revision.EvaluatorDisplayName, revision.FinalizedAt, revision.OverallNote, criteria);
                })
                .OrderByDescending(item => item.Stage == nameof(LearningAssessmentStage.Final))
                .ThenByDescending(item => item.FinalizedAt)
                .ToList();
            var currentStage = stages.Any(item => item.Stage == nameof(LearningAssessmentStage.Final))
                ? nameof(LearningAssessmentStage.Final)
                : stages.Any(item => item.Stage == nameof(LearningAssessmentStage.Middle))
                    ? nameof(LearningAssessmentStage.Middle)
                    : null;
            var progressState = placementAssessments.Any(item => item.Status == LearningAssessmentStatus.Reopened)
                ? "CorrectionInProgress"
                : currentStage is null
                ? "AwaitingMiddle"
                : currentStage == nameof(LearningAssessmentStage.Final) ? "FinalComplete" : "MiddleComplete";
            records.Add(new StudentLearningRecordPlacementDto(
                placement.PlacementId, placement.CompanyName, placement.PeriodName, placement.StartDate, placement.EndDate,
                progressState, currentStage, stages, monitoringByPlacement.GetValueOrDefault(placement.PlacementId, []),
                legacyByPlacement.GetValueOrDefault(placement.PlacementId)));
        }

        return records;
    }

    private sealed record StudentPlacementProjection(
        Guid PlacementId,
        Guid TenantId,
        Guid CompanyId,
        Guid PeriodId,
        string CompanyName,
        string PeriodName,
        DateOnly StartDate,
        DateOnly EndDate);

    private sealed record StudentAssessmentProjection(
        Guid AssessmentId,
        Guid PlacementId,
        LearningAssessmentStage Stage,
        LearningAssessmentStatus Status,
        Guid RevisionId);

    private sealed record StudentEvidenceProjection(
        Guid AssessmentId,
        Guid CriterionSnapshotId,
        Guid JournalEntryId,
        string Text,
        DateTimeOffset SubmittedAt);

    private sealed record StudentMonitoringProjection(
        Guid PlacementId,
        Guid Id,
        LearningRecordMonitoringStatus Status,
        string? Note,
        string? FollowUpContext,
        DateTimeOffset CreatedAt);

    private sealed record StudentLegacyFinalAssessmentProjection(
        Guid PlacementId,
        Guid AssessmentId,
        decimal? FinalScore,
        DateTimeOffset? FinalizedAt);

}
