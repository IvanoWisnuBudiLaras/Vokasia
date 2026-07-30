namespace Vokasia.Infrastructure.Email;

/// <summary>
/// VOK-H4-E3 §2 — pesan email SUDAH DI-RENDER (subject/html/text), siap kirim. Rendering (template
/// + model -> teks) SENGAJA dipisah dari pengiriman (lihat EmailTemplateRenderer) - IEmailSender
/// tidak perlu tahu apa pun soal template, hanya cara kirim + cara jaga idempotency.
///
/// IdempotencyKey = MassTransit MessageId dari consumer pemanggil (SELALU tersedia, tak bergantung
/// apakah ada Notification in-app terkait - lihat doc-comment SentEmail utk alasan lengkap kenapa
/// bukan NotificationId yang dipakai sbg kunci utama).
/// </summary>
public sealed record EmailMessage(string ToEmail, string TemplateId, string Subject, string Html, string Text, Guid IdempotencyKey);
