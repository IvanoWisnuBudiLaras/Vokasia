using System.Data;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Vokasia.Api.Auth;
using Vokasia.Api.Authorization;
using Vokasia.Api.Validation;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Identity;
using Vokasia.Infrastructure.Persistence;
using Vokasia.Infrastructure.Queries;

namespace Vokasia.Api.Endpoints;

public record LearningAssessmentDraftCriterionInput(
    Guid CriterionSnapshotId,
    int? Score,
    string? Comment,
    List<Guid> JournalEntryIds);

public record LearningAssessmentDraftRequest(string? OverallNote, List<LearningAssessmentDraftCriterionInput> Criteria);
public record LearningAssessmentReopenRequest(string? Reason);
public record LearningAssessmentDraftTransferRequest(string? Reason);
public record LearningAssessmentEvidenceDto(Guid JournalEntryId, string Text, DateTimeOffset SubmittedAt);
public record LearningAssessmentEvidenceCandidateDto(Guid JournalEntryId, string Text, DateTimeOffset SubmittedAt);
public record LearningAssessmentCriterionDto(Guid CriterionSnapshotId, string Name, string Description, int SortOrder, int? Score, string? Comment, List<LearningAssessmentEvidenceDto> Evidence);
public record LearningAssessmentMiddleContextDto(bool Available, string? Status, string? OperationalState);
public record LearningAssessmentDto(
    Guid PlacementId,
    string Stage,
    string Status,
    string OperationalState,
    string OperationalStateLabel,
    string? OverallNote,
    DateTimeOffset? FinalizedAt,
    List<LearningAssessmentCriterionDto> Criteria,
    List<LearningAssessmentEvidenceCandidateDto> EvidenceCandidates,
    LearningAssessmentMiddleContextDto? MiddleContext);

/// <summary>
/// Private V3 Mentor assessment lifecycle. It is intentionally isolated from the legacy V2
/// weighted assessment routes: this flow stores only 1-5 criterion observations and never a
/// combined score.
/// </summary>
public static class LearningAssessmentEndpoints
{
    public static IEndpointRouteBuilder MapLearningAssessmentEndpoints(this IEndpointRouteBuilder app)
    {
        var assessments = app.MapGroup("/api/placements/{placementId:guid}/learning-assessments")
            .WithTags("Learning Record")
            .AddEndpointFilter<ValidationFilter>();
        assessments.MapGet("/{stage}", GetStage).RequireAuthorization();
        assessments.MapPut("/{stage}/draft", SaveDraft).RequireAuthorization();
        assessments.MapPost("/{stage}/finalize", FinalizeAssessment).RequireAuthorization();
        assessments.MapPost("/{stage}/reopen", ReopenAssessment).RequireAuthorization(RbacPolicies.TenantAdminOnly);
        assessments.MapPost("/{stage}/draft/transfer", TransferDraft).RequireAuthorization(RbacPolicies.TenantAdminOnly);
        return app;
    }

    private static async Task<IResult> ReopenAssessment(
        Guid placementId,
        LearningAssessmentStage stage,
        LearningAssessmentReopenRequest request,
        ITenantContext tenant,
        VokasiaDbContext db,
        CancellationToken ct)
    {
        if (!tenant.TenantId.HasValue || !tenant.UserId.HasValue)
        {
            return Results.Forbid();
        }
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Results.UnprocessableEntity(new { message = "Alasan reopen wajib diisi." });
        }

        var assessment = await (
            from item in db.LearningAssessments
            join placement in db.Placements on item.PlacementId equals placement.Id
            where item.PlacementId == placementId && item.Stage == stage && placement.TenantId == tenant.TenantId.Value
            select item).SingleOrDefaultAsync(ct);
        if (assessment is null)
        {
            return Results.NotFound();
        }
        if (assessment.Status == LearningAssessmentStatus.Reopened)
        {
            return Results.Ok(new { assessmentId = assessment.Id, stage = stage.ToString(), status = assessment.Status.ToString(), assessment.LatestFinalizedRevisionId });
        }
        if (assessment.Status != LearningAssessmentStatus.Finalized || !assessment.LatestFinalizedRevisionId.HasValue)
        {
            return Results.Conflict(new { code = "learning-assessment-not-finalized", message = "Hanya assessment finalized yang dapat dibuka kembali." });
        }

        assessment.Status = LearningAssessmentStatus.Reopened;
        assessment.ReopenedAt = DateTimeOffset.UtcNow;
        var reason = request.Reason.Trim();
        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(), TenantId = tenant.TenantId.Value, ActorUserId = tenant.UserId.Value,
            Action = "LearningAssessmentReopened", Entity = nameof(LearningAssessment), EntityId = assessment.Id.ToString(),
            MetaJson = JsonSerializer.Serialize(new { assessmentId = assessment.Id, placementId, stage = stage.ToString(), reason, latestFinalizedRevisionId = assessment.LatestFinalizedRevisionId }),
        });
        db.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(), Type = "LearningAssessmentReopened",
            PayloadJson = JsonSerializer.Serialize(new { assessmentId = assessment.Id, placementId, stage = stage.ToString(), reason }),
        });
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { assessmentId = assessment.Id, stage = stage.ToString(), status = assessment.Status.ToString(), assessment.LatestFinalizedRevisionId });
    }

    private static async Task<IResult> TransferDraft(
        Guid placementId,
        LearningAssessmentStage stage,
        LearningAssessmentDraftTransferRequest request,
        ITenantContext tenant,
        VokasiaDbContext db,
        CancellationToken ct)
    {
        if (!tenant.TenantId.HasValue || !tenant.UserId.HasValue)
        {
            return Results.Forbid();
        }
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Results.UnprocessableEntity(new { message = "Alasan transfer draft wajib diisi." });
        }

        var placement = await db.Placements.SingleOrDefaultAsync(item => item.Id == placementId, ct);
        if (placement is null)
        {
            return Results.NotFound();
        }
        if (!placement.MentorUserId.HasValue)
        {
            return Results.Conflict(new { code = "learning-assessment-no-current-mentor", message = "Placement belum memiliki mentor pengganti." });
        }

        var assessment = await db.LearningAssessments.SingleOrDefaultAsync(item => item.PlacementId == placementId && item.Stage == stage, ct);
        if (assessment is null)
        {
            return Results.NotFound();
        }
        if (assessment.Status == LearningAssessmentStatus.Finalized)
        {
            return Results.Conflict(new { code = "learning-assessment-finalized-immutable", message = "Assessment finalized tidak memiliki draft yang dapat ditransfer." });
        }
        if (assessment.EvaluatorUserId == placement.MentorUserId)
        {
            return Results.Conflict(new { code = "learning-assessment-draft-already-owned", message = "Draft sudah dimiliki mentor saat ini." });
        }

        var previousEvaluatorUserId = assessment.EvaluatorUserId;
        assessment.EvaluatorUserId = placement.MentorUserId;
        var reason = request.Reason.Trim();
        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(), TenantId = tenant.TenantId.Value, ActorUserId = tenant.UserId.Value,
            Action = "LearningAssessmentDraftTransferred", Entity = nameof(LearningAssessment), EntityId = assessment.Id.ToString(),
            MetaJson = JsonSerializer.Serialize(new { assessmentId = assessment.Id, placementId, stage = stage.ToString(), previousEvaluatorUserId, newEvaluatorUserId = placement.MentorUserId, reason }),
        });
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { assessmentId = assessment.Id, stage = stage.ToString(), status = assessment.Status.ToString(), evaluatorUserId = assessment.EvaluatorUserId });
    }

    private static async Task<IResult> GetStage(
        Guid placementId,
        LearningAssessmentStage stage,
        ClaimsPrincipal user,
        ITenantContext tenant,
        IAuthorizationService authorizationService,
        LearningRecordQueryService queries,
        VokasiaDbContext db,
        CancellationToken ct)
    {
        var access = await GetAssignedMentorPlacementAsync(placementId, user, tenant, authorizationService, db, ct);
        if (access.Failure is not null)
        {
            return access.Failure;
        }

        var ownership = await db.LearningAssessments.AsNoTracking()
            .Where(item => item.PlacementId == placementId && item.Stage == stage)
            .Select(item => new { item.Status, item.EvaluatorUserId })
            .SingleOrDefaultAsync(ct);
        if (ownership is not null && ownership.Status != LearningAssessmentStatus.Finalized && ownership.EvaluatorUserId != tenant.UserId)
        {
            return Results.Conflict(new
            {
                code = "learning-assessment-draft-transfer-required",
                message = "Draft assessment menunggu transfer eksplisit dari TenantAdmin sebelum dapat dibaca mentor pengganti.",
            });
        }

        var projection = await queries.GetStageAsync(placementId, stage, Today(), ct);
        if (projection is null)
        {
            return Results.NotFound();
        }
        if (projection.Snapshot is null)
        {
            return Results.UnprocessableEntity(new { message = "Placement belum memiliki snapshot Learning Record." });
        }

        return Results.Ok(ToDto(projection, stage, await queries.GetMiddleContextAsync(placementId, Today(), ct)));
    }

    private static async Task<IResult> SaveDraft(
        Guid placementId,
        LearningAssessmentStage stage,
        LearningAssessmentDraftRequest request,
        ClaimsPrincipal user,
        ITenantContext tenant,
        IAuthorizationService authorizationService,
        VokasiaDbContext db,
        LearningRecordQueryService queries,
        CancellationToken ct)
    {
        var access = await GetAssignedMentorPlacementAsync(placementId, user, tenant, authorizationService, db, ct);
        if (access.Failure is not null)
        {
            return access.Failure;
        }

        try
        {
            await using IDbContextTransaction? transaction = db.Database.IsRelational()
                ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct)
                : null;
            var placement = await db.Placements.SingleAsync(item => item.Id == placementId, ct);
            var period = await db.Periods.AsNoTracking().SingleAsync(item => item.Id == placement.PeriodId, ct);
            var snapshot = await db.PlacementLearningRecordSnapshots.Include(item => item.Criteria)
                .SingleOrDefaultAsync(item => item.PlacementId == placement.Id, ct);
            if (snapshot is null)
            {
                return Results.UnprocessableEntity(new { message = "Placement belum memiliki snapshot Learning Record." });
            }

            var assessment = await db.LearningAssessments.Include(item => item.DraftCriteria)
                .SingleOrDefaultAsync(item => item.PlacementId == placement.Id && item.Stage == stage, ct);
            if (assessment is { Status: LearningAssessmentStatus.Finalized })
            {
                return Results.Conflict(new { code = "learning-assessment-finalized-immutable", message = "Assessment yang sudah finalized tidak dapat diubah." });
            }
            if (assessment is not null && assessment.EvaluatorUserId.HasValue && assessment.EvaluatorUserId != tenant.UserId)
            {
                return Results.Conflict(new { code = "learning-assessment-draft-owned", message = "Draft assessment masih terikat pada evaluator sebelumnya." });
            }
            var availabilityFailure = ValidateMutationAvailability(
                stage, assessment?.Status ?? LearningAssessmentStatus.Draft, period.StartDate, period.EndDate);
            if (availabilityFailure is not null)
            {
                return availabilityFailure;
            }

            var criteriaById = snapshot.Criteria.ToDictionary(item => item.Id);
            if (request.Criteria.Any(item => !criteriaById.ContainsKey(item.CriterionSnapshotId)))
            {
                return Results.UnprocessableEntity(new { message = "Kriteria tidak berasal dari snapshot placement ini." });
            }

            if (assessment is null)
            {
                assessment = new LearningAssessment
                {
                    Id = Guid.NewGuid(), TenantId = placement.TenantId, PlacementId = placement.Id, SnapshotId = snapshot.Id,
                    Stage = stage, Status = LearningAssessmentStatus.Draft, EvaluatorUserId = tenant.UserId,
                };
                db.LearningAssessments.Add(assessment);
            }

            var evidenceIds = request.Criteria.SelectMany(item => item.JournalEntryIds).Distinct().ToList();
            var approvedJournalIds = evidenceIds.Count == 0
                ? new HashSet<Guid>()
                : (await db.JournalEntries.AsNoTracking()
                    .Where(item => evidenceIds.Contains(item.Id) && item.TenantId == placement.TenantId && item.PlacementId == placement.Id && item.Status == JournalEntryStatus.Approved)
                    .Select(item => item.Id)
                    .ToListAsync(ct)).ToHashSet();
            if (approvedJournalIds.Count != evidenceIds.Count)
            {
                return Results.UnprocessableEntity(new { message = "Evidence harus berupa jurnal Approved dari siswa dan placement yang sama." });
            }

            var existingCriteria = assessment.DraftCriteria.ToDictionary(item => item.CriterionSnapshotId);
            foreach (var input in request.Criteria)
            {
                if (!existingCriteria.TryGetValue(input.CriterionSnapshotId, out var draftCriterion))
                {
                    draftCriterion = new LearningAssessmentDraftCriterion
                    {
                        Id = Guid.NewGuid(), TenantId = placement.TenantId, AssessmentId = assessment.Id,
                        CriterionSnapshotId = input.CriterionSnapshotId,
                    };
                    assessment.DraftCriteria.Add(draftCriterion);
                    existingCriteria[input.CriterionSnapshotId] = draftCriterion;
                }
                draftCriterion.Score = input.Score;
                draftCriterion.Comment = input.Comment;
                var oldEvidence = await db.LearningAssessmentCriterionEvidence
                    .Where(item => item.DraftCriterionId == draftCriterion.Id)
                    .ToListAsync(ct);
                db.LearningAssessmentCriterionEvidence.RemoveRange(oldEvidence);
                db.LearningAssessmentCriterionEvidence.AddRange(input.JournalEntryIds.Select(journalEntryId => new LearningAssessmentCriterionEvidence
                {
                    Id = Guid.NewGuid(), TenantId = placement.TenantId, DraftCriterionId = draftCriterion.Id, JournalEntryId = journalEntryId,
                }));
            }
            assessment.OverallNote = request.OverallNote;
            await db.SaveChangesAsync(ct);
            if (transaction is not null)
            {
                await transaction.CommitAsync(ct);
            }
        }
        catch (Exception exception) when (HasSerializationFailure(exception))
        {
            db.ChangeTracker.Clear();
            return await SaveSerializationConflictAsync(queries, placementId, stage, ct);
        }

        var projection = await queries.GetStageAsync(placementId, stage, Today(), ct);
        return Results.Ok(ToDto(projection!, stage, await queries.GetMiddleContextAsync(placementId, Today(), ct)));
    }

    private static async Task<IResult> FinalizeAssessment(
        Guid placementId,
        LearningAssessmentStage stage,
        ClaimsPrincipal user,
        ITenantContext tenant,
        IAuthorizationService authorizationService,
        UserManager<AppUser> userManager,
        VokasiaDbContext db,
        LearningRecordQueryService queries,
        CancellationToken ct)
    {
        var access = await GetAssignedMentorPlacementAsync(placementId, user, tenant, authorizationService, db, ct);
        if (access.Failure is not null)
        {
            return access.Failure;
        }

        try
        {
            await using IDbContextTransaction? transaction = db.Database.IsRelational()
                ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct)
                : null;
            var placement = await db.Placements.SingleAsync(item => item.Id == placementId, ct);
            var period = await db.Periods.AsNoTracking().SingleAsync(item => item.Id == placement.PeriodId, ct);
            var assessment = await db.LearningAssessments.Include(item => item.DraftCriteria)
                .SingleOrDefaultAsync(item => item.PlacementId == placement.Id && item.Stage == stage, ct);
            if (assessment is { Status: LearningAssessmentStatus.Finalized })
            {
                return await FinalizedResultOrRetryableConflictAsync(queries, placementId, stage, ct);
            }
            var availabilityFailure = ValidateMutationAvailability(
                stage, assessment?.Status ?? LearningAssessmentStatus.Draft, period.StartDate, period.EndDate);
            if (availabilityFailure is not null)
            {
                return availabilityFailure;
            }
            if (assessment is null)
            {
                return Results.UnprocessableEntity(new { message = "Simpan draft assessment sebelum finalisasi." });
            }
            if (assessment.EvaluatorUserId != tenant.UserId)
            {
                return Results.Conflict(new { code = "learning-assessment-draft-owned", message = "Draft assessment masih terikat pada evaluator sebelumnya." });
            }

            var snapshotCriteria = await db.PlacementLearningRecordCriterionSnapshots.AsNoTracking()
                .Where(item => item.SnapshotId == assessment.SnapshotId).OrderBy(item => item.SortOrder).ToListAsync(ct);
            var byCriterion = assessment.DraftCriteria.ToDictionary(item => item.CriterionSnapshotId);
            var missing = snapshotCriteria.Where(item => !byCriterion.TryGetValue(item.Id, out var draft) || !draft.Score.HasValue).ToList();
            if (missing.Count > 0 || string.IsNullOrWhiteSpace(assessment.OverallNote))
            {
                return Results.UnprocessableEntity(new
                {
                    message = "Semua skor kriteria dan catatan keseluruhan wajib diisi sebelum finalisasi.",
                    missingCriterionIds = missing.Select(item => item.Id),
                    overallNoteRequired = string.IsNullOrWhiteSpace(assessment.OverallNote),
                });
            }

            var draftEvidence = await (
                from link in db.LearningAssessmentCriterionEvidence.AsNoTracking()
                join journal in db.JournalEntries.AsNoTracking() on link.JournalEntryId equals journal.Id
                join draft in db.LearningAssessmentDraftCriteria.AsNoTracking() on link.DraftCriterionId equals draft.Id
                where draft.AssessmentId == assessment.Id
                    && journal.TenantId == placement.TenantId
                    && journal.PlacementId == placement.Id
                    && journal.Status == JournalEntryStatus.Approved
                select new
                {
                    link.DraftCriterionId,
                    JournalEntryId = journal.Id,
                    journal.Text,
                    journal.SubmittedAt,
                }).ToListAsync(ct);
            var evidenceByDraftCriterion = draftEvidence
                .GroupBy(item => item.DraftCriterionId)
                .ToDictionary(group => group.Key, group => group.ToList());
            var revisionCriteria = snapshotCriteria.Select(item =>
                LearningAssessmentRevisionCriterion.Create(
                    placement.TenantId, item.Id, byCriterion[item.Id].Score!.Value, byCriterion[item.Id].Comment)).ToList();
            foreach (var revisionCriterion in revisionCriteria)
            {
                var draftCriterion = byCriterion[revisionCriterion.CriterionSnapshotId];
                foreach (var evidence in evidenceByDraftCriterion.GetValueOrDefault(draftCriterion.Id, []))
                {
                    revisionCriterion.AddEvidence(evidence.JournalEntryId, evidence.Text, evidence.SubmittedAt);
                }
            }

            var evaluator = await userManager.FindByIdAsync(tenant.UserId!.Value.ToString());
            if (evaluator is null)
            {
                return Results.Forbid();
            }
            var revision = LearningAssessmentRevision.Create(
                placement.TenantId, assessment.Id, placement.Id, assessment.SnapshotId, stage, tenant.UserId.Value,
                evaluator.FullName, assessment.OverallNote.Trim(), DateTimeOffset.UtcNow,
                revisionCriteria);
            assessment.Status = LearningAssessmentStatus.Finalized;
            assessment.LatestFinalizedRevisionId = revision.Id;
            db.LearningAssessmentRevisions.Add(revision);
            db.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(), TenantId = placement.TenantId, ActorUserId = tenant.UserId.Value,
                Action = "LearningAssessmentFinalized", Entity = nameof(LearningAssessment), EntityId = assessment.Id.ToString(),
                MetaJson = JsonSerializer.Serialize(new { assessmentId = assessment.Id, placementId = placement.Id, stage = stage.ToString(), revisionId = revision.Id }),
            });
            db.OutboxMessages.Add(new OutboxMessage
            {
                Id = Guid.NewGuid(), Type = "LearningAssessmentFinalized",
                PayloadJson = JsonSerializer.Serialize(new { assessmentId = assessment.Id, placementId = placement.Id, stage = stage.ToString(), revisionId = revision.Id }),
            });
            await db.SaveChangesAsync(ct);
            if (transaction is not null)
            {
                await transaction.CommitAsync(ct);
            }
        }
        catch (Exception exception) when (HasSerializationFailure(exception))
        {
            db.ChangeTracker.Clear();
            return await FinalizedResultOrRetryableConflictAsync(queries, placementId, stage, ct);
        }

        return await FinalizedResultOrRetryableConflictAsync(queries, placementId, stage, ct);
    }

    private static async Task<IResult> SaveSerializationConflictAsync(LearningRecordQueryService queries, Guid placementId, LearningAssessmentStage stage, CancellationToken ct)
    {
        var projection = await queries.GetStageAsync(placementId, stage, Today(), ct);
        return projection is { Assessment.Status: LearningAssessmentStatus.Finalized }
            ? Results.Conflict(new { code = "learning-assessment-finalized-immutable", message = "Assessment yang sudah finalized tidak dapat diubah." })
            : RetryableFinalizeConflict();
    }

    private static async Task<IResult> FinalizedResultOrRetryableConflictAsync(LearningRecordQueryService queries, Guid placementId, LearningAssessmentStage stage, CancellationToken ct)
    {
        var projection = await queries.GetStageAsync(placementId, stage, Today(), ct);
        if (projection is null || projection.Snapshot is null)
        {
            return Results.NotFound();
        }
        if (projection.Assessment is not { Status: LearningAssessmentStatus.Finalized } || projection.LatestRevision is null)
        {
            return RetryableFinalizeConflict();
        }

        return Results.Ok(ToDto(projection, stage, await queries.GetMiddleContextAsync(placementId, Today(), ct)));
    }

    private static IResult RetryableFinalizeConflict() => Results.Conflict(new
    {
        code = "learning-assessment-finalize-retryable",
        message = "Finalisasi belum selesai secara konsisten. Silakan coba lagi.",
        retryable = true,
    });

    private static async Task<MentorPlacementAccess> GetAssignedMentorPlacementAsync(
        Guid placementId,
        ClaimsPrincipal user,
        ITenantContext tenant,
        IAuthorizationService authorizationService,
        VokasiaDbContext db,
        CancellationToken ct)
    {
        if (tenant.Role != nameof(UserRole.IndustryMentor) || !tenant.UserId.HasValue)
        {
            return new MentorPlacementAccess(null, Results.Forbid());
        }
        var placement = await db.Placements.AsNoTracking().SingleOrDefaultAsync(item => item.Id == placementId, ct);
        if (placement is null)
        {
            return new MentorPlacementAccess(null, Results.NotFound());
        }
        if (placement.MentorUserId != tenant.UserId.Value ||
            !(await authorizationService.AuthorizeAsync(user, placement, RbacPolicies.MentorOwnPlacement)).Succeeded)
        {
            return new MentorPlacementAccess(null, Results.Forbid());
        }
        return new MentorPlacementAccess(placement, null);
    }

    private static LearningAssessmentDto ToDto(
        LearningAssessmentStageProjection projection,
        LearningAssessmentStage stage,
        LearningAssessmentMiddleContextProjection middleContext)
    {
        var status = projection.Assessment?.Status ?? LearningAssessmentStatus.Draft;
        var state = LearningRecordRules.GetOperationalState(stage, status, projection.Placement.StartDate, projection.Placement.EndDate, Today());
        var draftByCriterion = projection.DraftCriteria.ToDictionary(item => item.CriterionSnapshotId);
        var revisionByCriterion = projection.Result.RevisionCriteria.ToDictionary(item => item.CriterionSnapshotId);
        var showLatestRevision = status == LearningAssessmentStatus.Finalized && projection.LatestRevision is not null;
        var evidenceByCriterion = projection.Result.Evidence.GroupBy(item => item.CriterionSnapshotId)
            .ToDictionary(group => group.Key, group => group.Select(item => new LearningAssessmentEvidenceDto(item.JournalEntryId, item.Text, item.SubmittedAt)).ToList());
        var criteria = projection.Snapshot!.Criteria.OrderBy(item => item.SortOrder).Select(criterion =>
        {
            var score = showLatestRevision
                ? revisionByCriterion.GetValueOrDefault(criterion.Id)?.Score
                : draftByCriterion.GetValueOrDefault(criterion.Id)?.Score;
            var comment = showLatestRevision
                ? revisionByCriterion.GetValueOrDefault(criterion.Id)?.Comment
                : draftByCriterion.GetValueOrDefault(criterion.Id)?.Comment;
            return new LearningAssessmentCriterionDto(criterion.Id, criterion.Name, criterion.Description, criterion.SortOrder, score, comment,
                evidenceByCriterion.GetValueOrDefault(criterion.Id) ?? []);
        }).ToList();
        var context = stage == LearningAssessmentStage.Final
            ? new LearningAssessmentMiddleContextDto(middleContext.Available, middleContext.Status, middleContext.OperationalState)
            : null;
        var candidates = projection.EvidenceCandidates
            .Select(item => new LearningAssessmentEvidenceCandidateDto(item.JournalEntryId, item.Text, item.SubmittedAt))
            .ToList();
        return new LearningAssessmentDto(
            projection.Placement.PlacementId, stage.ToString(), status.ToString(), state.ToString(), LearningRecordRules.GetOperationalStateLabel(state),
            showLatestRevision ? projection.LatestRevision!.OverallNote : projection.Assessment?.OverallNote,
            showLatestRevision ? projection.LatestRevision!.FinalizedAt : null, criteria, candidates, context);
    }

    private static bool HasSerializationFailure(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure })
            {
                return true;
            }
        }

        return false;
    }

    private static IResult? ValidateMutationAvailability(
        LearningAssessmentStage stage,
        LearningAssessmentStatus status,
        DateOnly startDate,
        DateOnly endDate)
    {
        var today = Today();
        var state = LearningRecordRules.GetOperationalState(stage, status, startDate, endDate, today);
        if (state != LearningAssessmentOperationalState.NotDue)
        {
            return null;
        }

        var availableFrom = LearningRecordRules.GetDueDate(stage, startDate, endDate);
        return Results.UnprocessableEntity(new
        {
            code = "learning-assessment-not-yet-available",
            message = $"Assessment {stage} belum tersedia untuk diisi.",
            operationalState = state.ToString(),
            availableFrom = availableFrom.ToString("yyyy-MM-dd"),
        });
    }

    private static DateOnly Today() => AppTimeZone.TodayJakarta();

    private sealed record MentorPlacementAccess(Placement? Placement, IResult? Failure);
}
