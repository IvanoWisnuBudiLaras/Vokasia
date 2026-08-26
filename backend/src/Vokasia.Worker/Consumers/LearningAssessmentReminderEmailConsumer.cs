using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vokasia.Domain.Events;
using Vokasia.Infrastructure.Email;
using Vokasia.Infrastructure.Messaging;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Worker.Consumers;

public sealed class LearningAssessmentReminderEmailConsumer(VokasiaDbContext db, IdempotencyGuard guard, IEmailSender emailSender, ILogger<LearningAssessmentReminderEmailConsumer> logger)
    : IConsumer<LearningAssessmentReminderEmailRequestedEvent>
{
    public const string Name = nameof(LearningAssessmentReminderEmailConsumer);

    public async Task Consume(ConsumeContext<LearningAssessmentReminderEmailRequestedEvent> context)
    {
        var messageId = context.MessageId ?? Guid.Empty;
        if (!await guard.EnsureNotProcessedAsync(Name, messageId, context.CancellationToken))
        {
            logger.LogInformation("{Consumer}: pesan {MessageId} sudah diproses sebelumnya, dilewati.", Name, messageId);
            return;
        }

        var message = context.Message;
        var email = await db.Users.AsNoTracking().Where(user => user.Id == message.RecipientUserId)
            .Select(user => user.Email).FirstOrDefaultAsync(context.CancellationToken);
        if (!string.IsNullOrWhiteSpace(email))
        {
            var (subject, html, text) = EmailTemplateRenderer.LearningRecordReminder(
                message.StudentName, message.Stage, message.ReminderType, message.DueDate);
            await emailSender.SendAsync(new EmailMessage(email, "LearningAssessmentReminder", subject, html, text, messageId), context.CancellationToken);
        }

        await db.SaveChangesAsync(context.CancellationToken);
    }
}
