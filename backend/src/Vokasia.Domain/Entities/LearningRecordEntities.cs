using Vokasia.Domain.Common;

namespace Vokasia.Domain.Entities;

public class LearningRecordTemplate : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public int Version { get; set; }
    public LearningRecordTemplateStatus Status { get; set; } = LearningRecordTemplateStatus.Draft;
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ActivatedAt { get; set; }
    public List<LearningRecordTemplateCriterion> Criteria { get; set; } = [];
}

public class LearningRecordTemplateCriterion : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid TemplateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public class PlacementLearningRecordSnapshot : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid PlacementId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid SourceTemplateId { get; set; }
    public int SourceTemplateVersion { get; set; }
    public string? CompanyDisplayName { get; set; }
    public string? PeriodDisplayName { get; set; }
    public DateOnly? PeriodStartDate { get; set; }
    public DateOnly? PeriodEndDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<PlacementLearningRecordCriterionSnapshot> Criteria { get; set; } = [];
}

public class PlacementLearningRecordCriterionSnapshot : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SnapshotId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public class LearningAssessment : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid PlacementId { get; set; }
    public Guid SnapshotId { get; set; }
    public LearningAssessmentStage Stage { get; set; }
    public LearningAssessmentStatus Status { get; set; } = LearningAssessmentStatus.Draft;
    public Guid? EvaluatorUserId { get; set; }
    public string? OverallNote { get; set; }
    public DateTimeOffset? ReopenedAt { get; set; }
    public Guid? LatestFinalizedRevisionId { get; set; }
    public LearningAssessmentRevision? LatestFinalizedRevision { get; set; }
    public List<LearningAssessmentDraftCriterion> DraftCriteria { get; set; } = [];
    public List<LearningAssessmentRevision> Revisions { get; set; } = [];

    public LearningAssessmentRevision? GetLatestFinalizedRevision() => Revisions
        .OrderByDescending(revision => revision.FinalizedAt)
        .FirstOrDefault();
}

public class LearningAssessmentDraftCriterion : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid AssessmentId { get; set; }
    public Guid CriterionSnapshotId { get; set; }
    public int? Score { get; set; }
    public string? Comment { get; set; }
    public List<LearningAssessmentCriterionEvidence> Evidence { get; set; } = [];
}

public class LearningAssessmentRevision : ITenantScoped
{
    private List<LearningAssessmentRevisionCriterion> _criteria = [];

    private LearningAssessmentRevision()
    {
    }

    private LearningAssessmentRevision(
        Guid tenantId,
        Guid assessmentId,
        Guid placementId,
        Guid snapshotId,
        LearningAssessmentStage stage,
        Guid evaluatorUserId,
        string evaluatorDisplayName,
        string overallNote,
        DateTimeOffset finalizedAt,
        IEnumerable<LearningAssessmentRevisionCriterion> criteria)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        AssessmentId = assessmentId;
        PlacementId = placementId;
        SnapshotId = snapshotId;
        Stage = stage;
        EvaluatorUserId = evaluatorUserId;
        EvaluatorDisplayName = evaluatorDisplayName;
        OverallNote = overallNote;
        FinalizedAt = finalizedAt;
        _criteria = criteria.Select(criterion => criterion.ForRevision(Id)).ToList();
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; set; }
    public Guid AssessmentId { get; private set; }
    public Guid PlacementId { get; private set; }
    public Guid SnapshotId { get; private set; }
    public LearningAssessmentStage Stage { get; private set; }
    public Guid EvaluatorUserId { get; private set; }
    public string EvaluatorDisplayName { get; private set; } = string.Empty;
    public string OverallNote { get; private set; } = string.Empty;
    public DateTimeOffset FinalizedAt { get; private set; }
    public IReadOnlyCollection<LearningAssessmentRevisionCriterion> Criteria => _criteria.AsReadOnly();

    public static LearningAssessmentRevision Create(
        Guid tenantId,
        Guid assessmentId,
        Guid placementId,
        Guid snapshotId,
        LearningAssessmentStage stage,
        Guid evaluatorUserId,
        string evaluatorDisplayName,
        string overallNote,
        DateTimeOffset finalizedAt,
        IEnumerable<LearningAssessmentRevisionCriterion> criteria) =>
        new(
            tenantId,
            assessmentId,
            placementId,
            snapshotId,
            stage,
            evaluatorUserId,
            evaluatorDisplayName,
            overallNote,
            finalizedAt,
            criteria);
}

public class LearningAssessmentRevisionCriterion : ITenantScoped
{
    private readonly List<LearningAssessmentRevisionCriterionEvidence> _evidence = [];

    private LearningAssessmentRevisionCriterion()
    {
    }

    private LearningAssessmentRevisionCriterion(
        Guid tenantId,
        Guid criterionSnapshotId,
        int score,
        string? comment,
        IEnumerable<LearningAssessmentRevisionCriterionEvidence>? evidence = null)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        CriterionSnapshotId = criterionSnapshotId;
        Score = score;
        Comment = comment;
        _evidence = evidence?.Select(item => item.ForCriterion(Id)).ToList() ?? [];
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; set; }
    public Guid RevisionId { get; private set; }
    public Guid CriterionSnapshotId { get; private set; }
    public int Score { get; private set; }
    public string? Comment { get; private set; }
    public IReadOnlyCollection<LearningAssessmentRevisionCriterionEvidence> Evidence => _evidence.AsReadOnly();

    public static LearningAssessmentRevisionCriterion Create(
        Guid tenantId,
        Guid criterionSnapshotId,
        int score,
        string? comment)
    {
        LearningRecordRules.ValidateScore(score);
        return new LearningAssessmentRevisionCriterion(tenantId, criterionSnapshotId, score, comment);
    }

    public void AddEvidence(Guid journalEntryId, string text, DateTimeOffset submittedAt) =>
        _evidence.Add(LearningAssessmentRevisionCriterionEvidence.Create(TenantId, Id, journalEntryId, text, submittedAt));

    internal LearningAssessmentRevisionCriterion ForRevision(Guid revisionId) => new(TenantId, CriterionSnapshotId, Score, Comment, _evidence)
    {
        RevisionId = revisionId
    };
}

public class LearningAssessmentRevisionCriterionEvidence : ITenantScoped
{
    private LearningAssessmentRevisionCriterionEvidence()
    {
    }

    private LearningAssessmentRevisionCriterionEvidence(
        Guid tenantId,
        Guid revisionCriterionId,
        Guid journalEntryId,
        string text,
        DateTimeOffset submittedAt)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        RevisionCriterionId = revisionCriterionId;
        JournalEntryId = journalEntryId;
        Text = text;
        SubmittedAt = submittedAt;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; set; }
    public Guid RevisionCriterionId { get; private set; }
    public Guid JournalEntryId { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public DateTimeOffset SubmittedAt { get; private set; }

    public static LearningAssessmentRevisionCriterionEvidence Create(
        Guid tenantId,
        Guid revisionCriterionId,
        Guid journalEntryId,
        string text,
        DateTimeOffset submittedAt) =>
        new(tenantId, revisionCriterionId, journalEntryId, text, submittedAt);

    internal LearningAssessmentRevisionCriterionEvidence ForCriterion(Guid revisionCriterionId) =>
        new(TenantId, revisionCriterionId, JournalEntryId, Text, SubmittedAt);
}

public class LearningAssessmentCriterionEvidence : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid DraftCriterionId { get; set; }
    public Guid? JournalEntryId { get; set; }
    public Guid? PortfolioEvidenceId { get; set; }
    public DateTimeOffset LinkedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class TeacherMonitoringEvent : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid PlacementId { get; set; }
    public Guid TeacherUserId { get; set; }
    public LearningRecordMonitoringStatus Status { get; set; }
    public string? Note { get; set; }
    public LearningRecordMonitoringVisibility Visibility { get; set; }
    public Guid? FollowUpVisitId { get; set; }
    public string? FollowUpContext { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class AssessmentReminderDelivery : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid PlacementId { get; set; }
    public LearningAssessmentStage Stage { get; set; }
    public LearningAssessmentReminderType ReminderType { get; set; }
    public Guid RecipientUserId { get; set; }
    public DateTimeOffset DeliveredAt { get; set; } = DateTimeOffset.UtcNow;
}
