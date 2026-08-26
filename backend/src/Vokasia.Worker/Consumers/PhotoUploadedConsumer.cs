using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;
using SixLabors.ImageSharp;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Domain.Events;
using Vokasia.Infrastructure.Messaging;
using Vokasia.Infrastructure.Persistence;
using Vokasia.Worker.Imaging;

namespace Vokasia.Worker.Consumers;

/// <summary>
/// VOK-H4-E1 §2 — unduh dari MinIO -> kompres ≤200KB -> strip EXIF (kecuali Tenant.GeotagAllowed)
/// -> thumbnail 320px -> simpan ThumbKey, Status=Processed. Gagal decode -> Status=Failed + notif
/// siswa (BUKAN crash - konsumsi pesan dianggap SELESAI, tak dilempar ke retry/DLQ MassTransit,
/// krn gambar korup tak akan pernah bisa di-decode betapa pun sering dicoba ulang). Kegagalan
/// KONEKSI MinIO (unduh/unggah) SEBALIKNYA dibiarkan throw tanpa ditangkap - itu transient, retry
/// MassTransit (5x exponential retry, VokasiaMassTransit.cs) memang utk kasus ini.
///
/// Bucket "Minio:Bucket" (fallback "vokasia-journal") - nilai SAMA PERSIS dgn JournalEndpoints.cs
/// (Vokasia.Api) tapi TIDAK bisa dibagi lewat konstanta bersama (JournalEndpoints.AllowedContentTypes
/// dst. bertanda `internal`, hanya terlihat dlm assembly Vokasia.Api - Worker assembly terpisah,
/// pola sama persis yg sudah didokumentasikan Guard/ValidatorCoverageTests.cs sesi H3-E3) -
/// duplikasi literal disengaja, dicatat di sini bukan diam-diam.
/// </summary>
public class PhotoUploadedConsumer(
    VokasiaDbContext db, IdempotencyGuard guard, INotifier notifier, IMinioClient minio,
    IConfiguration config, ILogger<PhotoUploadedConsumer> logger)
    : IConsumer<PhotoUploadedEvent>
{
    public const string Name = nameof(PhotoUploadedConsumer);
    private const string BucketConfigKey = "Minio:Bucket";
    private const string DefaultBucket = "vokasia-journal";

    public async Task Consume(ConsumeContext<PhotoUploadedEvent> context)
    {
        var ct = context.CancellationToken;
        var messageId = context.MessageId ?? Guid.Empty;

        if (!await guard.EnsureNotProcessedAsync(Name, messageId, ct))
        {
            logger.LogInformation("{Consumer}: pesan {MessageId} sudah diproses sebelumnya, dilewati.", Name, messageId);
            return;
        }

        var msg = context.Message;

        var photo = await db.JournalPhotos.FirstOrDefaultAsync(p => p.Id == msg.PhotoId, ct);
        if (photo is null)
        {
            logger.LogWarning("{Consumer}: JournalPhoto {PhotoId} tak ditemukan - dilewati.", Name, msg.PhotoId);
            await db.SaveChangesAsync(ct);
            return;
        }

        var photoTenantId = await db.JournalEntries.AsNoTracking()
            .Where(e => e.Id == msg.JournalEntryId)
            .Join(db.Placements.AsNoTracking(), e => e.PlacementId, p => p.Id, (e, p) => p.TenantId)
            .FirstOrDefaultAsync(ct);
        if (photoTenantId == Guid.Empty || !ObjectStorageKeyPolicy.IsOwnedKey(photo.ObjectKey, photoTenantId, "journal"))
        {
            photo.Status = PhotoStatus.Failed;
            await db.SaveChangesAsync(ct);
            logger.LogWarning("{Consumer}: object key foto {PhotoId} tidak berada pada prefix tenant yang sah.", Name, msg.PhotoId);
            return;
        }

        var geotagAllowed = await db.JournalEntries.AsNoTracking().Where(e => e.Id == msg.JournalEntryId)
            .Join(db.Placements.AsNoTracking(), e => e.PlacementId, p => p.Id, (e, p) => p.TenantId)
            .Join(db.Tenants.AsNoTracking(), tid => tid, t => t.Id, (tid, t) => t.GeotagAllowed)
            .FirstOrDefaultAsync(ct);

        var bucket = config[BucketConfigKey] ?? DefaultBucket;

        var original = new MemoryStream();
        await minio.GetObjectAsync(new GetObjectArgs()
            .WithBucket(bucket)
            .WithObject(photo.ObjectKey)
            .WithCallbackStream(stream => stream.CopyTo(original)), ct);
        original.Position = 0;

        byte[] processedBytes;
        byte[] thumbBytes;
        try
        {
            // Strip SELURUH profil metadata (bukan cuma sub-tag GPS) - lebih aman & lebih
            // sederhana drpd bedah tag GPS satu-satu lintas format JPEG/PNG/WEBP (lihat
            // doc-comment Tenant.GeotagAllowed). Logika decode/strip/encode/thumbnail ada di
            // PhotoProcessor (Vokasia.Worker/Imaging) - diekstrak murni supaya testable tanpa
            // MinIO nyata, lihat doc-comment kelas itu.
            var result = PhotoProcessor.Process(original.ToArray(), geotagAllowed);
            processedBytes = result.Processed;
            thumbBytes = result.Thumbnail;
        }
        catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException)
        {
            // Permanen (file korup/bukan gambar) - retry tak akan menolong. Tandai Failed + notif,
            // JANGAN rethrow (MassTransit menganggap pesan ini SELESAI diproses, bukan gagal).
            photo.Status = PhotoStatus.Failed;

            var studentUserId = await db.JournalEntries.AsNoTracking().Where(e => e.Id == msg.JournalEntryId)
                .Join(db.Placements.AsNoTracking(), e => e.PlacementId, p => p.Id, (e, p) => p.StudentId)
                .Join(db.Students.AsNoTracking(), sid => sid, s => s.Id, (sid, s) => s.UserId)
                .FirstOrDefaultAsync(ct);
            if (studentUserId is not null)
            {
                notifier.CreateNotification(studentUserId.Value, NotificationType.PhotoProcessingFailed, new { PhotoId = msg.PhotoId });
            }

            // The object was accepted into the tenant-owned private prefix but failed byte-level
            // image validation. Remove it so a permanently rejected upload cannot accumulate as
            // an orphan. Storage failures remain transient and are intentionally rethrown.
            await minio.RemoveObjectAsync(new RemoveObjectArgs()
                .WithBucket(bucket)
                .WithObject(photo.ObjectKey), ct);

            await db.SaveChangesAsync(ct);
            logger.LogWarning(ex, "{Consumer}: foto {PhotoId} gagal decode (korup/bukan gambar) - Status=Failed, notif siswa.", Name, msg.PhotoId);
            return;
        }

        var thumbKey = photo.ObjectKey + "-thumb.jpg";

        using (var uploadMain = new MemoryStream(processedBytes))
        {
            await minio.PutObjectAsync(new PutObjectArgs()
                .WithBucket(bucket)
                .WithObject(photo.ObjectKey)
                .WithStreamData(uploadMain)
                .WithObjectSize(uploadMain.Length)
                .WithContentType("image/jpeg"), ct);
        }

        using (var uploadThumb = new MemoryStream(thumbBytes))
        {
            await minio.PutObjectAsync(new PutObjectArgs()
                .WithBucket(bucket)
                .WithObject(thumbKey)
                .WithStreamData(uploadThumb)
                .WithObjectSize(uploadThumb.Length)
                .WithContentType("image/jpeg"), ct);
        }

        photo.ThumbKey = thumbKey;
        photo.Status = PhotoStatus.Processed;
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "{Consumer}: foto {PhotoId} diproses ({Size} bytes, geotag {Geotag}) -> thumb {ThumbKey}.",
            Name, msg.PhotoId, processedBytes.Length, geotagAllowed ? "diizinkan" : "dihapus", thumbKey);
    }
}
