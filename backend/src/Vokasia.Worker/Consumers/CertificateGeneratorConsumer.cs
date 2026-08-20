using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;
using QuestPDF.Fluent;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Domain.Events;
using Vokasia.Infrastructure.Messaging;
using Vokasia.Infrastructure.Persistence;
using Vokasia.Worker.Export;

namespace Vokasia.Worker.Consumers;

/// <summary>
/// VOK-H5-E1 §5 — GenerateCertificatePdf(placementId): QuestPDF (identitas+durasi+nilai+QR), CertCode 12 kar acak
/// (CertCodeGenerator, retry loop kalau tabrakan - astronomically rare tapi ditangani, bukan
/// diasumsikan tak pernah terjadi), simpan MinIO + baris Certificate.
///
/// Idempoten: kalau Certificate utk PlacementId ini SUDAH ada (mis. event terkirim dobel), skip
/// - TIDAK generate ulang / tidak timpa CertCode yang sudah diterbitkan (CertCode publik, kalau
/// sempat dibagikan/dicetak, mengubahnya diam-diam akan merusak link yang sudah beredar).
/// </summary>
public class CertificateGeneratorConsumer(
    VokasiaDbContext db, IdempotencyGuard guard, IMinioClient minio, IConfiguration config,
    ILogger<CertificateGeneratorConsumer> logger)
    : IConsumer<CertificateRequestedEvent>
{
    public const string Name = nameof(CertificateGeneratorConsumer);
    private const string BucketConfigKey = "Minio:Bucket";
    private const string DefaultBucket = "vokasia-journal";
    private const int MaxCertCodeAttempts = 5;

    public async Task Consume(ConsumeContext<CertificateRequestedEvent> context)
    {
        var ct = context.CancellationToken;
        var messageId = context.MessageId ?? Guid.Empty;

        if (!await guard.EnsureNotProcessedAsync(Name, messageId, ct))
        {
            logger.LogInformation("{Consumer}: pesan {MessageId} sudah diproses sebelumnya, dilewati.", Name, messageId);
            return;
        }

        var msg = context.Message;

        var alreadyExists = await db.Certificates.AnyAsync(c => c.PlacementId == msg.PlacementId, ct);
        if (alreadyExists)
        {
            logger.LogInformation("{Consumer}: placement {PlacementId} sudah punya sertifikat - dilewati (idempoten).", Name, msg.PlacementId);
            await db.SaveChangesAsync(ct);
            return;
        }

        var info = await (
            from p in db.Placements.AsNoTracking()
            where p.Id == msg.PlacementId
            join s in db.Students.AsNoTracking() on p.StudentId equals s.Id
            join c in db.Companies.AsNoTracking() on p.CompanyId equals c.Id
            join per in db.Periods.AsNoTracking() on p.PeriodId equals per.Id
            join t in db.Tenants.AsNoTracking() on p.TenantId equals t.Id
            join a in db.Assessments.AsNoTracking() on p.Id equals a.PlacementId into aj
            from a in aj.DefaultIfEmpty()
            select new { s.FullName, CompanyName = c.Name, per.Name, per.StartDate, per.EndDate, t.SchoolName, FinalScore = (decimal?)a.FinalScore }
            ).FirstOrDefaultAsync(ct);

        if (info is null)
        {
            logger.LogWarning("{Consumer}: placement {PlacementId} tak ditemukan - dilewati.", Name, msg.PlacementId);
            await db.SaveChangesAsync(ct);
            return;
        }

        var certCode = await GenerateUniqueCertCodeAsync(ct);
        var publicUrl = config["Frontend:PublicUrl"] ?? config["NEXT_PUBLIC_APP_URL"] ?? "http://localhost:3000";
        var verifyUrl = $"{publicUrl.TrimEnd('/')}/verify/{certCode}";

        var pdfData = new CertificateData(info.FullName, info.SchoolName, info.CompanyName, info.Name, info.StartDate, info.EndDate, info.FinalScore, certCode, verifyUrl);
        var pdfBytes = new CertificatePdfDocument(pdfData).GeneratePdf();

        var bucket = config[BucketConfigKey] ?? DefaultBucket;
        var objectKey = $"tenant/{msg.TenantId}/certificates/{msg.PlacementId}.pdf";

        using (var uploadStream = new MemoryStream(pdfBytes))
        {
            await minio.PutObjectAsync(new PutObjectArgs()
                .WithBucket(bucket)
                .WithObject(objectKey)
                .WithStreamData(uploadStream)
                .WithObjectSize(uploadStream.Length)
                .WithContentType("application/pdf"), ct);
        }

        db.Certificates.Add(new Certificate
        {
            Id = Guid.NewGuid(), TenantId = msg.TenantId, PlacementId = msg.PlacementId,
            CertCode = certCode, PdfKey = objectKey,
        });

        await db.SaveChangesAsync(ct);
        logger.LogInformation("{Consumer}: sertifikat placement {PlacementId} diterbitkan (CertCode {CertCode}) -> {ObjectKey}.", Name, msg.PlacementId, certCode, objectKey);
    }

    /// <summary>Retry loop, bukan asumsi "12 kar acak pasti unik" - tabrakan astronomically rare tapi DITANGANI (unique constraint DB akan tetap jadi jaring pengaman terakhir kalau 5x tetap tabrakan, sangat tak mungkin).</summary>
    private async Task<string> GenerateUniqueCertCodeAsync(CancellationToken ct)
    {
        for (var attempt = 0; attempt < MaxCertCodeAttempts; attempt++)
        {
            var candidate = CertCodeGenerator.Generate();
            var exists = await db.Certificates.AsNoTracking().AnyAsync(c => c.CertCode == candidate, ct);
            if (!exists)
            {
                return candidate;
            }
        }
        // Praktis tak akan pernah sampai sini (62^12 ruang kemungkinan) - kalau sampai, generate satu lagi tanpa cek ulang drpd macet permanen; unique constraint DB (kalau ada) akan jadi jaring terakhir.
        return CertCodeGenerator.Generate();
    }
}
