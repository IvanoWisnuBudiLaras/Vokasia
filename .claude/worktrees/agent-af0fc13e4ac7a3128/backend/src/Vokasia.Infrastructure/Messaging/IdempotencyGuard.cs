using Microsoft.EntityFrameworkCore;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Infrastructure.Messaging;

/// <summary>
/// VOK-H4-E1 — penanda idempotency consumer (AC: "message sama dikirim 2x -> efek 1x"). Dipanggil
/// di AWAL setiap consumer, SEBELUM efek samping apa pun (side effect: tulis Notification, proses
/// foto MinIO, dst.).
///
/// Desain SENGAJA "check lalu add-tanpa-save" (bukan langsung SaveChanges sendiri): baris
/// ProcessedMessage yang ditambahkan di sini HANYA masuk ChangeTracker, BELUM di-commit -
/// consumer WAJIB panggil db.SaveChangesAsync() SENDIRI di akhir (setelah efek sampingnya
/// selesai ditulis ke ChangeTracker yang SAMA), menyatukan penanda idempotency + efek bisnis
/// dalam SATU transaksi atomik. Kalau consumer gagal/throw SEBELUM SaveChanges miliknya sendiri,
/// penanda ini IKUT batal (rollback implisit) - retry MassTransit berikutnya akan dianggap BELUM
/// diproses lagi (benar: efek sampingnya juga belum benar-benar tersimpan).
///
/// [KETERBATASAN dicatat, bukan diam-diam]: cek AnyAsync di sini adalah fast-path OPTIMISTIC, bukan
/// jaminan tunggal thd race TRUE-CONCURRENT (dua delivery pesan yang SAMA diproses nyaris
/// bersamaan oleh 2 instance/thread consumer - keduanya lolos AnyAsync sebelum salah satu commit).
/// Constraint PK (ConsumerName,MessageId) di DB akan menolak SALAH SATU SaveChangesAsync -nya
/// (DbUpdateException) kalau race itu benar terjadi, TAPI efek samping bisnis pemenang race yang
/// KALAH sudah terlanjur dieksekusi in-memory sebelum SaveChanges-nya sendiri gagal (tak
/// otomatis "dibatalkan" scr aplikatif, hanya baris DB-nya yang gagal tersimpan berkat rollback
/// transaksi EF Core). Diterima sbg trade-off: skenario AC ticket ("message sama dikirim 2x") pada
/// praktiknya adalah REDELIVERY SETELAH GAGAL/timeout (sekuensial, bukan benar-benar simultan) -
/// tercakup penuh oleh desain ini. Concurrent-duplicate murni adalah edge case langka yang butuh
/// deteksi jenis exception Postgres spesifik (unique violation) utk ditutup total - kompleksitas
/// tambahan yang tak sepadan utk skenario yang tak diminta AC secara eksplisit.
/// </summary>
public class IdempotencyGuard(VokasiaDbContext db)
{
    public async Task<bool> EnsureNotProcessedAsync(string consumerName, Guid messageId, CancellationToken ct)
    {
        var already = await db.ProcessedMessages.AsNoTracking()
            .AnyAsync(p => p.ConsumerName == consumerName && p.MessageId == messageId, ct);
        if (already)
        {
            return false;
        }

        db.ProcessedMessages.Add(new ProcessedMessage { ConsumerName = consumerName, MessageId = messageId });
        return true;
    }
}
