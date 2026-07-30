using Microsoft.Extensions.Logging;

namespace Vokasia.Infrastructure.Email;

/// <summary>
/// VOK-H4-E3 §2 "env Development → tulis ke log/folder `.emails/`" — pengirim dev: TIDAK benar2
/// mengirim SMTP apa pun, cukup log + tulis 1 file .html per email ke folder `.emails/` (default
/// relatif ke working directory proses; override via env `EMAIL_DEV_FOLDER` kalau perlu) supaya
/// bisa "dibuka" (dilihat) sbg bukti render nyata - itulah literal DoD ticket ("5 email hasil
/// render (folder .emails/) -> setor"), BUKAN mailbox SMTP sungguhan (Mailpit dst - infra BARU yg
/// tak diminta ticket ini, lihat DECISIONS.md D29 utk alasan lengkap kenapa tidak ditambah).
///
/// Idempotency (SentEmail) TETAP diterapkan via decorator IdempotentEmailSender di LUAR kelas ini -
/// kelas ini sendiri TIDAK perlu tahu soal itu (dipanggil hanya kalau memang belum pernah terkirim).
/// </summary>
public class DevLogEmailSender(ILogger<DevLogEmailSender> logger) : IEmailSender
{
    private readonly string _folder = Environment.GetEnvironmentVariable("EMAIL_DEV_FOLDER") is { Length: > 0 } f ? f : ".emails";

    public async Task<bool> SendAsync(EmailMessage message, CancellationToken ct)
    {
        Directory.CreateDirectory(_folder);
        var fileName = $"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}_{message.TemplateId}_{message.IdempotencyKey:N}.html";
        var path = Path.Combine(_folder, fileName);

        var content = $"""
            <!-- To: {message.ToEmail} -->
            <!-- Subject: {message.Subject} -->
            <!-- TemplateId: {message.TemplateId} -->
            {message.Html}
            <!--
            === plain-text fallback ===
            {message.Text}
            -->
            """;

        await File.WriteAllTextAsync(path, content, ct);
        logger.LogInformation(
            "[DevLogEmailSender] Email {TemplateId} -> {To} ditulis ke {Path} (bukan SMTP nyata, dev-only).",
            message.TemplateId, message.ToEmail, path);
        return true;
    }
}
