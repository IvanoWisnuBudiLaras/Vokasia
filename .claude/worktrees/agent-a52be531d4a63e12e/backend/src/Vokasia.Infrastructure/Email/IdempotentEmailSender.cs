using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Infrastructure.Email;

/// <summary>
/// VOK-H4-E3 §2 AC "given email di-retry, then email terkirim 1x per notifikasi (bukti SentEmail)".
/// Decorator: bungkus IEmailSender KONKRET (DevLog/Smtp) - consumer HANYA bergantung pada
/// interface ini (didaftarkan sbg IEmailSender di DI, lihat DependencyInjection.cs), tak tahu ada
/// pembungkus idempotency sama sekali.
///
/// Urutan SENGAJA: cek dulu (skip kalau sudah ada) -> KIRIM dulu -> BARU catat SentEmail +
/// SaveChangesAsync SENDIRI (terpisah dari SaveChanges consumer pemanggil di akhir). Alasan urutan
/// ini (bukan catat-dulu-baru-kirim): kalau proses crash SETELAH catat tapi SEBELUM kirim benar2
/// sukses, hasilnya email TAK PERNAH terkirim SAMA SEKALI (silent data loss, AC ticket jelas2 minta
/// "terkirim 1x", BUKAN "terkirim 0x atau 1x") - kirim-dulu menerima trade-off sebaliknya (jendela
/// crash sempit antara ack SMTP & 1 INSERT bisa berujung kirim 2x pd kasus SANGAT jarang) yang jauh
/// lebih bisa diterima drpd email hilang diam-diam. Lihat doc-comment SentEmail utk perbandingan
/// lengkap dgn ProcessedMessage/IdempotencyGuard punya trade-off yg SAMA filosofinya.
/// </summary>
public class IdempotentEmailSender(VokasiaDbContext db, IEmailSender inner, ILogger<IdempotentEmailSender> logger) : IEmailSender
{
    public async Task<bool> SendAsync(EmailMessage message, CancellationToken ct)
    {
        var already = await db.SentEmails.AsNoTracking()
            .AnyAsync(x => x.IdempotencyKey == message.IdempotencyKey, ct);
        if (already)
        {
            logger.LogInformation(
                "[IdempotentEmailSender] {TemplateId} -> {To} (kunci {Key}) sudah pernah terkirim - dilewati (idempoten, bukan error).",
                message.TemplateId, message.ToEmail, message.IdempotencyKey);
            return false;
        }

        var sent = await inner.SendAsync(message, ct);
        if (!sent)
        {
            return false; // inner sender sendiri menolak (mis. SmtpEmailSender tanpa config) - JANGAN dicatat SentEmail (belum benar2 terkirim).
        }

        db.SentEmails.Add(new SentEmail
        {
            Id = Guid.NewGuid(),
            IdempotencyKey = message.IdempotencyKey,
            TemplateId = message.TemplateId,
            ToEmail = message.ToEmail,
        });

        try
        {
            await db.SaveChangesAsync(ct); // SENDIRI, terpisah dari SaveChanges consumer pemanggil - lihat doc-comment kelas.
        }
        catch (DbUpdateException)
        {
            // Race sangat jarang: 2 pengiriman utk kunci SAMA nyaris bersamaan, keduanya lolos
            // AnyAsync di atas sebelum salah satu commit (trade-off SAMA PERSIS spt IdempotencyGuard,
            // lihat doc-comment-nya) - unique index IdempotencyKey menolak salah satu INSERT.
            // Email TETAP terkirim (sisi baik: tak hilang) tp tercatat 2x krn DB constraint gagal
            // di sisi yg kalah race - diterima sbg edge case langka, bukan bug.
            logger.LogWarning("[IdempotentEmailSender] SaveChanges gagal (kemungkinan race unique IdempotencyKey) utk {Key} - email SUDAH terkirim, dicatat pemenang race saja.", message.IdempotencyKey);
        }

        return true;
    }
}
