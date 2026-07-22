using Microsoft.Extensions.Logging;

namespace Vokasia.Infrastructure.Messaging;

/// <summary>
/// VOK-H4-E1 §2 MentorInvitedConsumer — interface siap pakai, implementasi SMTP/Resend sungguhan =
/// H4-E3 (belum ada infra email di proyek ini sama sekali, konsisten dgn gap yang SAMA persis
/// sudah dicatat MagicLinkService.CreateInviteAsync sejak VOK-H2-E3 D21). Sementara: log dev-only.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string body, CancellationToken ct);
}

public class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string toEmail, string subject, string body, CancellationToken ct)
    {
        logger.LogInformation("[dev-only, tanpa infra email sampai H4-E3] Email ke {To} - {Subject}: {Body}", toEmail, subject, body);
        return Task.CompletedTask;
    }
}
