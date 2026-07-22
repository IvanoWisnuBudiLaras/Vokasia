using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using MimeKit.Text;

namespace Vokasia.Infrastructure.Email;

/// <summary>
/// VOK-H4-E3 §2 "implementasi SmtpEmailSender (MailKit ke SMTP/Resend, config env)" — dipakai saat
/// BUKAN Development (lihat DependencyInjection.cs). Config baca langsung dari `config["Smtp:*"]`
/// (env `Smtp__Host` dst, SUDAH ada di .env sejak H1 scaffold, konsisten dgn pola RabbitMq__Host/
/// Minio__Endpoint di file ini juga - tanpa Options-binding class terpisah, sama gaya seluruh
/// proyek). Resend (disebut ticket sbg alternatif) juga bicara SMTP standar di port 587 dgn API key
/// sbg username/password - TIDAK butuh kode terpisah, config env yang beda saja sudah cukup.
///
/// TIDAK ada jaringan SMTP nyata di lingkungan dev sesi ini (Smtp__Host kosong di .env) - kelas ini
/// DITULIS+di-compile tapi jalur DevLogEmailSender yang benar2 dieksekusi sepanjang sesi (env
/// Development, lihat DependencyInjection.cs). Dicatat eksplisit, bukan diklaim "sudah ditest kirim
/// SMTP sungguhan" - itu di luar scope sandbox ini (tak ada mailserver publik utk dites aman).
/// </summary>
public class SmtpEmailSender(IConfiguration config, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task<bool> SendAsync(EmailMessage message, CancellationToken ct)
    {
        var host = config["Smtp:Host"];
        if (string.IsNullOrWhiteSpace(host))
        {
            logger.LogWarning("[SmtpEmailSender] Smtp:Host kosong - email {TemplateId} -> {To} TIDAK dikirim (config belum diisi).", message.TemplateId, message.ToEmail);
            return false;
        }

        var port = int.TryParse(config["Smtp:Port"], out var p) ? p : 587;
        var username = config["Smtp:Username"];
        var password = config["Smtp:Password"];
        var from = config["Smtp:From"] ?? "no-reply@vokasia.local";

        var mime = new MimeMessage();
        mime.From.Add(MailboxAddress.Parse(from));
        mime.To.Add(MailboxAddress.Parse(message.ToEmail));
        mime.Subject = message.Subject;

        var body = new BodyBuilder { HtmlBody = message.Html, TextBody = message.Text };
        mime.Body = body.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(host, port, SecureSocketOptions.StartTlsWhenAvailable, ct);
        if (!string.IsNullOrWhiteSpace(username))
        {
            await client.AuthenticateAsync(username, password, ct);
        }
        await client.SendAsync(mime, ct);
        await client.DisconnectAsync(true, ct);

        logger.LogInformation("[SmtpEmailSender] Email {TemplateId} -> {To} terkirim via {Host}:{Port}.", message.TemplateId, message.ToEmail, host, port);
        return true;
    }
}
