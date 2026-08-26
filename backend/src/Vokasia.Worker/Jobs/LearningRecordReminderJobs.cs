using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Messaging;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Worker.Jobs;

public sealed class LearningRecordReminderJobs(VokasiaDbContext db, INotifier notifier, ILogger<LearningRecordReminderJobs> logger)
{
    public async Task EnqueueMentorReminders(DateOnly? runDate = null)
    {
        var today = runDate ?? AppTimeZone.TodayJakarta();
        var placements = await (
            from placement in db.Placements.IgnoreQueryFilters().AsNoTracking()
            join period in db.Periods.IgnoreQueryFilters().AsNoTracking() on placement.PeriodId equals period.Id
            join snapshot in db.PlacementLearningRecordSnapshots.IgnoreQueryFilters().AsNoTracking() on placement.Id equals snapshot.PlacementId
            join student in db.Students.IgnoreQueryFilters().AsNoTracking() on placement.StudentId equals student.Id
            where placement.Status == PlacementStatus.Active && placement.MentorUserId.HasValue
            select new ReminderPlacement(placement.Id, placement.TenantId, placement.MentorUserId!.Value, student.FullName, period.StartDate, period.EndDate)
        ).ToListAsync();

        if (placements.Count == 0)
        {
            logger.LogInformation("EnqueueMentorReminders: {Date} tidak ada placement Learning Record aktif.", today);
            return;
        }

        var placementIds = placements.Select(item => item.PlacementId).ToList();
        var finalized = (await db.LearningAssessments.IgnoreQueryFilters().AsNoTracking()
            .Where(item => placementIds.Contains(item.PlacementId) && item.Status == LearningAssessmentStatus.Finalized)
            .Select(item => new { item.PlacementId, item.Stage })
            .ToListAsync())
            .Select(item => (item.PlacementId, item.Stage))
            .ToHashSet();
        var delivered = (await db.AssessmentReminderDeliveries.IgnoreQueryFilters().AsNoTracking()
            .Where(item => placementIds.Contains(item.PlacementId))
            .Select(item => new { item.PlacementId, item.Stage, item.ReminderType, item.RecipientUserId })
            .ToListAsync())
            .Select(item => (item.PlacementId, item.Stage, item.ReminderType, item.RecipientUserId))
            .ToHashSet();

        var created = 0;
        foreach (var placement in placements)
        {
            foreach (var stage in new[] { LearningAssessmentStage.Middle, LearningAssessmentStage.Final })
            {
                if (finalized.Contains((placement.PlacementId, stage))) continue;

                var state = LearningRecordRules.GetOperationalState(stage, LearningAssessmentStatus.Draft, placement.StartDate, placement.EndDate, today);
                var reminderType = state switch
                {
                    LearningAssessmentOperationalState.Due => LearningAssessmentReminderType.Due,
                    LearningAssessmentOperationalState.Overdue => LearningAssessmentReminderType.Overdue,
                    _ => (LearningAssessmentReminderType?)null,
                };
                if (!reminderType.HasValue || delivered.Contains((placement.PlacementId, stage, reminderType.Value, placement.MentorUserId))) continue;

                if (await TryCreateReminderAsync(placement, stage, reminderType.Value))
                {
                    created++;
                    delivered.Add((placement.PlacementId, stage, reminderType.Value, placement.MentorUserId));
                }
            }
        }

        logger.LogInformation("EnqueueMentorReminders: {Date} -> {Count} work item Learning Record dibuat.", today, created);
    }

    private async Task<bool> TryCreateReminderAsync(ReminderPlacement placement, LearningAssessmentStage stage, LearningAssessmentReminderType reminderType)
    {
        if (await db.AssessmentReminderDeliveries.IgnoreQueryFilters().AsNoTracking().AnyAsync(item =>
            item.PlacementId == placement.PlacementId && item.Stage == stage && item.ReminderType == reminderType && item.RecipientUserId == placement.MentorUserId))
        {
            return false;
        }

        var dueDate = LearningRecordRules.GetDueDate(stage, placement.StartDate, placement.EndDate);
        db.AssessmentReminderDeliveries.Add(new AssessmentReminderDelivery
        {
            Id = Guid.NewGuid(), TenantId = placement.TenantId, PlacementId = placement.PlacementId,
            Stage = stage, ReminderType = reminderType, RecipientUserId = placement.MentorUserId,
        });
        notifier.CreateNotification(placement.MentorUserId, NotificationType.LearningAssessmentReminder, new
        {
            placementId = placement.PlacementId, stage = stage.ToString(), reminderType = reminderType.ToString(), dueDate = dueDate.ToString("yyyy-MM-dd"),
        });
        db.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(), Type = "LearningAssessmentReminderEmailRequested",
            PayloadJson = JsonSerializer.Serialize(new
            {
                recipientUserId = placement.MentorUserId, placementId = placement.PlacementId, studentName = placement.StudentName,
                stage, reminderType, dueDate,
            }),
        });

        try
        {
            await db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException exception) when (IsAssessmentReminderDeliveryUniqueViolation(exception))
        {
            db.ChangeTracker.Clear();
            logger.LogInformation("EnqueueMentorReminders: delivery {PlacementId}/{Stage}/{ReminderType}/{Recipient} dimenangkan worker lain.", placement.PlacementId, stage, reminderType, placement.MentorUserId);
            return false;
        }
    }

    private static bool IsAssessmentReminderDeliveryUniqueViolation(DbUpdateException exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres)
            {
                return postgres.SqlState == PostgresErrorCodes.UniqueViolation &&
                       postgres.ConstraintName == "IX_AssessmentReminderDeliveries_PlacementId_Stage_ReminderType~";
            }
        }

        return false;
    }

    private sealed record ReminderPlacement(Guid PlacementId, Guid TenantId, Guid MentorUserId, string StudentName, DateOnly StartDate, DateOnly EndDate);
}
