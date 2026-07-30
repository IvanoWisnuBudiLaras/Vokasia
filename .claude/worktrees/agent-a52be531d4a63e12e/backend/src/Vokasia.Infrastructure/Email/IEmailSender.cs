namespace Vokasia.Infrastructure.Email;

/// <summary>
/// VOK-H4-E3 §2 — kontrak tunggal pengiriman email. Menggantikan
/// <c>Vokasia.Infrastructure.Messaging.IEmailSender</c> (H4-E1, hanya (to,subject,body) mentah,
/// TANPA idempotency/template - lihat doc-comment lama di file itu: "implementasi SMTP/Resend
/// sungguhan = H4-E3"). Interface LAMA di Messaging/IEmailSender.cs DIHAPUS (bukan dibiarkan
/// nganggur di samping yang baru) - satu kontrak, satu jalur, satu tempat cari kalau perlu ubah.
///
/// Return bool ("apakah benar-benar terkirim baru") - AC ticket "retry tidak menduplikasi": caller
/// (consumer) bisa log berbeda utk "terkirim baru" vs "dilewati krn sudah pernah" tanpa exception
/// khusus, tetap idempoten (bukan error) kalau dipanggil ulang dgn IdempotencyKey yang sama.
/// </summary>
public interface IEmailSender
{
    Task<bool> SendAsync(EmailMessage message, CancellationToken ct);
}
