using Microsoft.EntityFrameworkCore;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Infrastructure.Queries;

/// <summary>
/// Read projections for private V3 assessments. The projection deliberately never joins
/// <see cref="JournalPhoto"/>: callers receive journal metadata only, never MinIO keys or media.
/// </summary>
public sealed class LearningRecordQueryService(VokasiaDbContext db)
{
    public const int MaxUnboundedExportRows = 5_000;

    public async Task<LearningRecordReportResult> ExecuteReportAsync(
        LearningRecordReportQuery query,
        CancellationToken ct)
    {
        if (query.Page < 1 || query.PageSize is not (25 or 50 or 100))
        {
            throw new ArgumentException("Report page size must be 25, 50, or 100.", nameof(query));
        }

        var report = ApplyReportFilters(query);
        var totalCount = await report.CountAsync(ct);
        var completeCount = await report.CountAsync(item => item.FinalStatus == LearningAssessmentStatus.Finalized, ct);
        var needsAttentionCount = await report.CountAsync(item =>
            item.MonitoringStatus == LearningRecordMonitoringStatus.NeedsAttention ||
            item.MonitoringStatus == LearningRecordMonitoringStatus.Problem, ct);
        var incompleteCount = totalCount - completeCount;

        var ordered = ApplyOrdering(report, query.Sort, query.Descending);
        var rows = await ordered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new LearningRecordReportResult(
            rows.Select(ToReportItem).ToList(),
            totalCount,
            new LearningRecordReportSummary(totalCount, completeCount, incompleteCount, needsAttentionCount));
    }

    public async Task<LearningRecordReportExportResult> ExecuteReportExportAsync(
        LearningRecordReportExportSpec spec,
        CancellationToken ct)
    {
        if (spec.Query.Page < 1 || spec.Query.PageSize is not (25 or 50 or 100))
        {
            throw new ArgumentException("Report export query page is invalid.", nameof(spec));
        }

        var report = ApplyReportFilters(spec.Query);
        var totalCount = await report.CountAsync(ct);
        if (!spec.Quantity.HasValue && totalCount > MaxUnboundedExportRows)
        {
            throw new InvalidOperationException($"Unbounded export exceeds the {MaxUnboundedExportRows}-row safety limit.");
        }
        var ordered = ApplyOrdering(report, spec.Query.Sort, spec.Query.Descending);
        var skip = spec.Scope == LearningRecordReportExportScope.CurrentPage
            ? (spec.Query.Page - 1) * spec.Query.PageSize
            : 0;
        var rowsQuery = ordered.Skip(skip);
        if (spec.Quantity.HasValue)
        {
            rowsQuery = rowsQuery.Take(spec.Quantity.Value);
        }

        var rows = await rowsQuery.ToListAsync(ct);
        return new LearningRecordReportExportResult(rows.Select(ToReportItem).ToList(), totalCount);
    }

    private IQueryable<LearningRecordReportProjection> ApplyReportFilters(LearningRecordReportQuery query)
    {
        var report = BuildReportQuery(query);
        if (query.PeriodId.HasValue)
        {
            report = report.Where(item => item.PeriodId == query.PeriodId.Value);
        }

        if (query.CompanyId.HasValue)
        {
            report = report.Where(item => item.CompanyId == query.CompanyId.Value);
        }

        if (query.AssessmentStage is { } stage)
        {
            report = stage == LearningAssessmentStage.Middle
                ? report.Where(item => item.MiddleStatus.HasValue)
                : report.Where(item => item.FinalStatus.HasValue);
        }

        if (query.AssessmentStatus is { } status)
        {
            report = query.AssessmentStage switch
            {
                LearningAssessmentStage.Middle => report.Where(item => item.MiddleStatus == status),
                LearningAssessmentStage.Final => report.Where(item => item.FinalStatus == status),
                _ => report.Where(item => item.MiddleStatus == status || item.FinalStatus == status),
            };
        }

        if (query.MonitoringStatus is { } monitoringStatus)
        {
            report = report.Where(item => item.MonitoringStatus == monitoringStatus);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            report = report.Where(item => item.StudentName.ToLower().Contains(search.ToLower()) || item.CompanyName.ToLower().Contains(search.ToLower()));
        }
        return report;
    }

    public async Task<LearningAssessmentStageProjection?> GetStageAsync(
        Guid placementId,
        LearningAssessmentStage stage,
        DateOnly today,
        CancellationToken ct)
    {
        var placement = await (
            from candidate in db.Placements.AsNoTracking()
            join period in db.Periods.AsNoTracking() on candidate.PeriodId equals period.Id
            where candidate.Id == placementId
            select new LearningAssessmentPlacementProjection(
                candidate.Id, candidate.TenantId, candidate.StudentId, candidate.CompanyId,
                period.StartDate, period.EndDate))
            .SingleOrDefaultAsync(ct);
        if (placement is null)
        {
            return null;
        }

        var snapshot = await db.PlacementLearningRecordSnapshots.AsNoTracking()
            .Include(item => item.Criteria)
            .SingleOrDefaultAsync(item => item.PlacementId == placementId, ct);
        if (snapshot is null)
        {
            return new LearningAssessmentStageProjection(placement, null, null, [], null, new LearningAssessmentResultProjection([], []), []);
        }

        var assessment = await db.LearningAssessments.AsNoTracking()
            .SingleOrDefaultAsync(item => item.PlacementId == placementId && item.Stage == stage, ct);
        var draftCriteria = assessment is null
            ? []
            : await db.LearningAssessmentDraftCriteria.AsNoTracking()
                .Where(item => item.AssessmentId == assessment.Id)
                .ToListAsync(ct);
        var latestRevision = assessment?.LatestFinalizedRevisionId is { } revisionId
            ? await db.LearningAssessmentRevisions.AsNoTracking().SingleOrDefaultAsync(item => item.Id == revisionId, ct)
            : null;
        var revisionCriteria = latestRevision is null
            ? []
            : await db.LearningAssessmentRevisionCriteria.AsNoTracking()
                .Include(item => item.Evidence)
                .Where(item => item.RevisionId == latestRevision.Id)
                .ToListAsync(ct);
        var draftEvidence = assessment is null
            ? []
            : await (
                from link in db.LearningAssessmentCriterionEvidence.AsNoTracking()
                join journal in db.JournalEntries.AsNoTracking() on link.JournalEntryId equals journal.Id
                join draft in db.LearningAssessmentDraftCriteria.AsNoTracking() on link.DraftCriterionId equals draft.Id
                where draft.AssessmentId == assessment.Id
                    && journal.TenantId == placement.TenantId
                    && journal.PlacementId == placement.PlacementId
                    && journal.Status == JournalEntryStatus.Approved
                select new LearningAssessmentEvidenceProjection(
                    draft.CriterionSnapshotId, journal.Id, journal.Text, journal.SubmittedAt))
                .ToListAsync(ct);
        var revisionEvidence = revisionCriteria
            .SelectMany(criterion => criterion.Evidence.Select(item =>
                new LearningAssessmentEvidenceProjection(
                    criterion.CriterionSnapshotId, item.JournalEntryId, item.Text, item.SubmittedAt)))
            .ToList();
        var evidence = assessment?.Status == LearningAssessmentStatus.Finalized && latestRevision is not null
            ? revisionEvidence
            : draftEvidence;
        var evidenceCandidates = await db.JournalEntries.AsNoTracking()
            .Where(item => item.TenantId == placement.TenantId && item.PlacementId == placement.PlacementId && item.Status == JournalEntryStatus.Approved)
            .OrderByDescending(item => item.SubmittedAt)
            .Select(item => new LearningAssessmentEvidenceCandidateProjection(item.Id, item.Text, item.SubmittedAt))
            .ToListAsync(ct);

        return new LearningAssessmentStageProjection(placement, snapshot, assessment, draftCriteria, latestRevision,
            new LearningAssessmentResultProjection(revisionCriteria, evidence), evidenceCandidates);
    }

    public async Task<LearningAssessmentMiddleContextProjection> GetMiddleContextAsync(Guid placementId, DateOnly today, CancellationToken ct)
    {
        var placement = await (
            from candidate in db.Placements.AsNoTracking()
            join period in db.Periods.AsNoTracking() on candidate.PeriodId equals period.Id
            where candidate.Id == placementId
            select new LearningAssessmentPlacementProjection(
                candidate.Id, candidate.TenantId, candidate.StudentId, candidate.CompanyId,
                period.StartDate, period.EndDate))
            .SingleOrDefaultAsync(ct);
        if (placement is null)
        {
            return new LearningAssessmentMiddleContextProjection(false, null, null);
        }

        var middle = await db.LearningAssessments.AsNoTracking()
            .SingleOrDefaultAsync(item => item.PlacementId == placementId && item.Stage == LearningAssessmentStage.Middle, ct);
        var state = LearningRecordRules.GetOperationalState(
            LearningAssessmentStage.Middle,
            middle?.Status ?? LearningAssessmentStatus.Draft,
            placement.StartDate,
            placement.EndDate,
            today);
        if (middle?.Status != LearningAssessmentStatus.Finalized || !middle.LatestFinalizedRevisionId.HasValue)
        {
            return new LearningAssessmentMiddleContextProjection(false, middle?.Status.ToString() ?? LearningAssessmentStatus.Draft.ToString(), state.ToString());
        }

        var hasRevision = await db.LearningAssessmentRevisions.AsNoTracking()
            .AnyAsync(item => item.Id == middle.LatestFinalizedRevisionId.Value, ct);
        return new LearningAssessmentMiddleContextProjection(
            hasRevision,
            middle.Status.ToString(),
            state.ToString());
    }

    private IQueryable<LearningRecordReportProjection> BuildReportQuery(LearningRecordReportQuery query)
    {
        var report =
            from placement in db.Placements.AsNoTracking()
            join student in db.Students.AsNoTracking() on placement.StudentId equals student.Id
            join company in db.Companies.AsNoTracking() on placement.CompanyId equals company.Id
            join period in db.Periods.AsNoTracking() on placement.PeriodId equals period.Id
            let middleStatus = db.LearningAssessments
                .Where(item => item.PlacementId == placement.Id && item.Stage == LearningAssessmentStage.Middle)
                .Select(item => (LearningAssessmentStatus?)item.Status)
                .FirstOrDefault()
            let finalStatus = db.LearningAssessments
                .Where(item => item.PlacementId == placement.Id && item.Stage == LearningAssessmentStage.Final)
                .Select(item => (LearningAssessmentStatus?)item.Status)
                .FirstOrDefault()
            let monitoringStatus = db.TeacherMonitoringEvents
                .Where(item => item.PlacementId == placement.Id)
                .OrderByDescending(item => item.CreatedAt)
                .ThenByDescending(item => item.Id)
                .Select(item => (LearningRecordMonitoringStatus?)item.Status)
                .FirstOrDefault()
            let monitoringUpdatedAt = db.TeacherMonitoringEvents
                .Where(item => item.PlacementId == placement.Id)
                .OrderByDescending(item => item.CreatedAt)
                .ThenByDescending(item => item.Id)
                .Select(item => (DateTimeOffset?)item.CreatedAt)
                .FirstOrDefault()
            where placement.TenantId == query.TenantId &&
                  (!query.TeacherId.HasValue || placement.TeacherId == query.TeacherId.Value)
            select new LearningRecordReportProjection
            {
                PlacementId = placement.Id,
                StudentName = student.FullName,
                CompanyId = company.Id,
                CompanyName = company.Name,
                PeriodId = period.Id,
                PeriodName = period.Name,
                PeriodStartDate = period.StartDate,
                PeriodEndDate = period.EndDate,
                MiddleStatus = middleStatus,
                FinalStatus = finalStatus,
                MonitoringStatus = monitoringStatus,
                MonitoringUpdatedAt = monitoringUpdatedAt,
            };

        return report;
    }

    private static IOrderedQueryable<LearningRecordReportProjection> ApplyOrdering(
        IQueryable<LearningRecordReportProjection> report,
        LearningRecordReportSort sort,
        bool descending) => sort switch
    {
        LearningRecordReportSort.CompanyName => descending
            ? report.OrderByDescending(item => item.CompanyName).ThenBy(item => item.PlacementId)
            : report.OrderBy(item => item.CompanyName).ThenBy(item => item.PlacementId),
        LearningRecordReportSort.PeriodName => descending
            ? report.OrderByDescending(item => item.PeriodName).ThenBy(item => item.PlacementId)
            : report.OrderBy(item => item.PeriodName).ThenBy(item => item.PlacementId),
        LearningRecordReportSort.MonitoringUpdatedAt => descending
            ? report.OrderByDescending(item => item.MonitoringUpdatedAt).ThenBy(item => item.PlacementId)
            : report.OrderBy(item => item.MonitoringUpdatedAt).ThenBy(item => item.PlacementId),
        _ => descending
            ? report.OrderByDescending(item => item.StudentName).ThenBy(item => item.PlacementId)
            : report.OrderBy(item => item.StudentName).ThenBy(item => item.PlacementId),
    };

    private static LearningRecordReportItem ToReportItem(LearningRecordReportProjection item) => new(
        item.PlacementId,
        item.StudentName,
        item.CompanyId,
        item.CompanyName,
        item.PeriodId,
        item.PeriodName,
        item.PeriodStartDate,
        item.PeriodEndDate,
        item.MiddleStatus,
        item.FinalStatus,
        item.MonitoringStatus,
        item.MonitoringUpdatedAt);
}

public enum LearningRecordReportSort
{
    StudentName,
    CompanyName,
    PeriodName,
    MonitoringUpdatedAt,
}

public sealed record LearningRecordReportQuery(
    Guid TenantId,
    Guid? TeacherId,
    Guid? PeriodId,
    Guid? CompanyId,
    LearningAssessmentStage? AssessmentStage,
    LearningAssessmentStatus? AssessmentStatus,
    LearningRecordMonitoringStatus? MonitoringStatus,
    string? Search,
    LearningRecordReportSort Sort,
    bool Descending,
    int Page,
    int PageSize);

public sealed record LearningRecordReportResult(
    IReadOnlyList<LearningRecordReportItem> Items,
    int TotalCount,
    LearningRecordReportSummary Summary);

public enum LearningRecordReportExportScope
{
    CurrentFilters,
    CurrentPage,
}

public sealed record LearningRecordReportExportSpec(
    LearningRecordReportQuery Query,
    LearningRecordReportExportScope Scope,
    int? Quantity);

public sealed record LearningRecordReportExportResult(
    IReadOnlyList<LearningRecordReportItem> Items,
    int TotalMatchingCount);

public sealed record LearningRecordReportSummary(
    int TotalCount,
    int CompleteCount,
    int IncompleteCount,
    int NeedsAttentionCount);

public sealed record LearningRecordReportItem(
    Guid PlacementId,
    string StudentName,
    Guid CompanyId,
    string CompanyName,
    Guid PeriodId,
    string PeriodName,
    DateOnly PeriodStartDate,
    DateOnly PeriodEndDate,
    LearningAssessmentStatus? MiddleStatus,
    LearningAssessmentStatus? FinalStatus,
    LearningRecordMonitoringStatus? MonitoringStatus,
    DateTimeOffset? MonitoringUpdatedAt);

internal sealed class LearningRecordReportProjection
{
    public Guid PlacementId { get; init; }
    public string StudentName { get; init; } = string.Empty;
    public Guid CompanyId { get; init; }
    public string CompanyName { get; init; } = string.Empty;
    public Guid PeriodId { get; init; }
    public string PeriodName { get; init; } = string.Empty;
    public DateOnly PeriodStartDate { get; init; }
    public DateOnly PeriodEndDate { get; init; }
    public LearningAssessmentStatus? MiddleStatus { get; init; }
    public LearningAssessmentStatus? FinalStatus { get; init; }
    public LearningRecordMonitoringStatus? MonitoringStatus { get; init; }
    public DateTimeOffset? MonitoringUpdatedAt { get; init; }
}

public sealed record LearningAssessmentPlacementProjection(
    Guid PlacementId,
    Guid TenantId,
    Guid StudentId,
    Guid CompanyId,
    DateOnly StartDate,
    DateOnly EndDate);

public sealed record LearningAssessmentEvidenceProjection(
    Guid CriterionSnapshotId,
    Guid JournalEntryId,
    string Text,
    DateTimeOffset SubmittedAt);

public sealed record LearningAssessmentResultProjection(
    IReadOnlyList<LearningAssessmentRevisionCriterion> RevisionCriteria,
    IReadOnlyList<LearningAssessmentEvidenceProjection> Evidence);

public sealed record LearningAssessmentEvidenceCandidateProjection(
    Guid JournalEntryId,
    string Text,
    DateTimeOffset SubmittedAt);

public sealed record LearningAssessmentStageProjection(
    LearningAssessmentPlacementProjection Placement,
    PlacementLearningRecordSnapshot? Snapshot,
    LearningAssessment? Assessment,
    IReadOnlyList<LearningAssessmentDraftCriterion> DraftCriteria,
    LearningAssessmentRevision? LatestRevision,
    LearningAssessmentResultProjection Result,
    IReadOnlyList<LearningAssessmentEvidenceCandidateProjection> EvidenceCandidates);

public sealed record LearningAssessmentMiddleContextProjection(
    bool Available,
    string? Status,
    string? OperationalState);
