namespace Vokasia.Domain.Entities;

/// <summary>
/// VOK-H4-E3 §2 "Idempoten per NotificationId" — lapisan idempotency KEDUA khusus pengiriman email,
/// TERPISAH dari <c>ProcessedMessage</c> (IdempotencyGuard, penanda level SELURUH consumer).
///
/// [ALASAN kenapa dua lapis, bukan cukup satu]: ProcessedMessage HANYA ter-commit bersamaan dgn
/// SaveChangesAsync() PENUTUP consumer (lihat doc-comment IdempotencyGuard) - kalau proses crash
/// SETELAH email benar2 terkirim (SMTP sukses) tapi SEBELUM SaveChangesAsync penutup itu, redelivery
/// MassTransit berikutnya akan menganggap pesan "belum diproses" (penanda ProcessedMessage ikut
/// rollback) -> consumer jalan lagi -> email terkirim KEDUA kalinya. SentEmail ditulis+di-commit
/// SENDIRI (SaveChanges terpisah) SEGERA setelah kirim sukses (lihat IdempotentEmailSender) - jendela
/// crash yg tersisa jauh lebih sempit (hanya antara "SMTP ack" dan "1 baris INSERT" itu sendiri,
/// bukan antara SMTP ack dan SELURUH efek samping consumer + SaveChanges-nya).
///
/// IdempotencyKey = kunci stabil per pesan yg diminta kirim (MassTransit MessageId - SELALU tersedia
/// tiap consumer, tak bergantung pada apakah event itu py Notification in-app terkait atau tidak,
/// lihat IEmailSender/EmailMessage). BUKAN Guid acak baru tiap percobaan kirim - kalau baru, unique
/// constraint di bawah tak pernah bisa mendeteksi percobaan kirim ulang utk pesan LOGIS yg sama.
/// </summary>
public class SentEmail
{
    public Guid Id { get; set; }
    public Guid IdempotencyKey { get; set; }
    public string TemplateId { get; set; } = default!;
    public string ToEmail { get; set; } = default!;
    public DateTimeOffset SentAt { get; set; } = DateTimeOffset.UtcNow;
}
