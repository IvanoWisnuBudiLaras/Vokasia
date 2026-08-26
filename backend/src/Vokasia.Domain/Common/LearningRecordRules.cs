namespace Vokasia.Domain.Common;

public static class LearningRecordRules
{
    public static void ValidateTemplateCriterionCount(int criterionCount)
    {
        if (criterionCount > 20)
        {
            throw new ArgumentException("A Learning Record template version may contain at most 20 criteria.", nameof(criterionCount));
        }
    }

    public static void ValidateScore(int score)
    {
        if (score is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(score), score, "Learning Record scores must be between 1 and 5.");
        }
    }

    public static string GetScoreLabel(int score)
    {
        ValidateScore(score);

        return score switch
        {
            1 => "Sangat Kurang",
            2 => "Kurang",
            3 => "Cukup",
            4 => "Baik",
            5 => "Sangat Baik",
            _ => throw new InvalidOperationException("Validated score was outside the supported range.")
        };
    }

    public static DateOnly GetDueDate(LearningAssessmentStage stage, DateOnly start, DateOnly end)
    {
        if (end < start)
        {
            throw new ArgumentException("Placement end date cannot be before its start date.", nameof(end));
        }

        return stage switch
        {
            LearningAssessmentStage.Middle => start.AddDays((end.DayNumber - start.DayNumber) / 2),
            LearningAssessmentStage.Final => end.AddDays(-7),
            _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unknown Learning Record assessment stage.")
        };
    }

    public static LearningAssessmentOperationalState GetOperationalState(
        LearningAssessmentStage stage,
        LearningAssessmentStatus status,
        DateOnly start,
        DateOnly end,
        DateOnly today)
    {
        if (status == LearningAssessmentStatus.Finalized)
        {
            return LearningAssessmentOperationalState.Complete;
        }

        var dueDate = GetDueDate(stage, start, end);
        if (today < dueDate)
        {
            return LearningAssessmentOperationalState.NotDue;
        }

        var isOverdue = stage == LearningAssessmentStage.Middle
            ? today > dueDate
            : today > end;

        return isOverdue
            ? LearningAssessmentOperationalState.Overdue
            : LearningAssessmentOperationalState.Due;
    }

    public static string GetOperationalStateLabel(LearningAssessmentOperationalState state) => state switch
    {
        LearningAssessmentOperationalState.NotDue => "Belum waktunya",
        LearningAssessmentOperationalState.Due => "Perlu diisi",
        LearningAssessmentOperationalState.Overdue => "Tertunda",
        LearningAssessmentOperationalState.Complete => "Selesai",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown Learning Record operational state.")
    };

    public static bool RequiresMonitoringNote(LearningRecordMonitoringStatus status) => status is
        LearningRecordMonitoringStatus.NeedsAttention or LearningRecordMonitoringStatus.Problem;

    public static void ValidateMonitoringNote(LearningRecordMonitoringStatus status, string? note)
    {
        if (RequiresMonitoringNote(status) && string.IsNullOrWhiteSpace(note))
        {
            throw new ArgumentException("A monitoring note is required for negative statuses.", nameof(note));
        }
    }
}
