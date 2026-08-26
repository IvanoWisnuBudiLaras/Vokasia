using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;
using QuestPDF.Fluent;
using System.Text.Json;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Domain.Events;
using Vokasia.Infrastructure.Email;
using Vokasia.Infrastructure.Messaging;
using Vokasia.Infrastructure.Persistence;
using Vokasia.Infrastructure.Queries;
using Vokasia.Worker.Export;

namespace Vokasia.Worker.Consumers;

/// <summary>
/// VOK-H5-E1 §4 — bangun file rekap (Xlsx hand-rolled/Pdf QuestPDF) -> MinIO -> notif +
/// email ExportReady (menutup gap D30 "ExportReady dirender tapi belum dipanggil consumer
/// produksi apa pun" - sekarang punya pemanggil nyata).
///
/// Legacy V2 grade-recap querying remains local to this Worker because it is an Api/Worker
/// compatibility seam. V3 Learning Record exports instead call the shared
/// LearningRecordQueryService semantic projection so UI, PDF, and XLSX cannot drift.
/// </summary>
public class ExportRequestedConsumer(
    VokasiaDbContext db, IdempotencyGuard guard, IMinioClient minio, IConfiguration config,
    INotifier notifier, IEmailSender emailSender, ILogger<ExportRequestedConsumer> logger)
    : IConsumer<ExportRequestedEvent>
{
    public const string Name = nameof(ExportRequestedConsumer);
    private const string BucketConfigKey = "Minio:Bucket";
    private const string DefaultBucket = "vokasia-journal";
    private const int PresignedExpirySeconds = 24 * 60 * 60; // 24 jam, sesuai AC ticket.

    public async Task Consume(ConsumeContext<ExportRequestedEvent> context)
    {
        var ct = context.CancellationToken;
        var messageId = context.MessageId ?? Guid.Empty;

        if (!await guard.EnsureNotProcessedAsync(Name, messageId, ct))
        {
            logger.LogInformation("{Consumer}: pesan {MessageId} sudah diproses sebelumnya, dilewati.", Name, messageId);
            return;
        }

        var msg = context.Message;
        var exportRequest = await db.ExportRequests.FirstOrDefaultAsync(e => e.Id == msg.Id, ct);
        if (exportRequest is null)
        {
            logger.LogWarning("{Consumer}: ExportRequest {Id} tak ditemukan - dilewati.", Name, msg.Id);
            await db.SaveChangesAsync(ct);
            return;
        }

        var period = await db.Periods.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == msg.PeriodId && p.TenantId == msg.TenantId, ct);
        var isV3Report = exportRequest.ReportKind == "V3LearningRecord";
        if ((!isV3Report && (period is null || exportRequest.PeriodId != msg.PeriodId)) || exportRequest.TenantId != msg.TenantId)
        {
            // Worker DbContexts intentionally run without an ambient tenant. Re-apply both tenant
            // and period ownership here so a malformed event cannot build a cross-tenant report.
            exportRequest.Status = ExportStatus.Failed;
            await db.SaveChangesAsync(ct);
            logger.LogWarning("{Consumer}: export {Id} tidak cocok dengan tenant/periode event - ditandai gagal.", Name, msg.Id);
            return;
        }

        byte[] fileBytes;
        string extension;
        string contentType;
        int rowCount;
        if (isV3Report)
        {
            GeneratedExport generated;
            try
            {
                generated = await BuildLearningRecordExportAsync(exportRequest, msg.TenantId, ct);
            }
            catch (InvalidOperationException ex)
            {
                exportRequest.Status = ExportStatus.Failed;
                await db.SaveChangesAsync(ct);
                logger.LogWarning(ex, "{Consumer}: V3 export {Id} memiliki konfigurasi tidak valid.", Name, exportRequest.Id);
                return;
            }
            fileBytes = generated.FileBytes;
            extension = generated.Extension;
            contentType = generated.ContentType;
            rowCount = generated.RowCount;
        }
        else
        {
            var rows = await BuildRecapRowsAsync(msg.TenantId, msg.PeriodId, ct);
            rowCount = rows.Count;
            if (exportRequest.Format == ExportFormat.Pdf)
            {
                var pdfRows = rows.Select(r => new GradeRecapPdfRow(r.StudentName, r.CompanyName, r.MentorAvg, r.TeacherAvg, r.FinalScore, r.Status)).ToList();
                fileBytes = new GradeRecapPdfDocument(period?.Name ?? "-", pdfRows).GeneratePdf();
                extension = "pdf";
                contentType = "application/pdf";
            }
            else
            {
                var headers = new[] { "Siswa", "Perusahaan", "Rata Mentor", "Rata Guru", "Nilai Akhir", "Status" };
                var xlsxRows = rows.Select(r => (IReadOnlyList<object?>)new object?[] { r.StudentName, r.CompanyName, r.MentorAvg, r.TeacherAvg, r.FinalScore, r.Status }).ToList();
                fileBytes = MinimalXlsxWriter.WriteSingleSheet(headers, xlsxRows);
                extension = "xlsx";
                contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            }
        }

        var bucket = config[BucketConfigKey] ?? DefaultBucket;
        var objectKey = $"tenant/{exportRequest.TenantId}/exports/{exportRequest.Id}.{extension}";

        using (var uploadStream = new MemoryStream(fileBytes))
        {
            await minio.PutObjectAsync(new PutObjectArgs()
                .WithBucket(bucket)
                .WithObject(objectKey)
                .WithStreamData(uploadStream)
                .WithObjectSize(uploadStream.Length)
                .WithContentType(contentType), ct);
        }

        exportRequest.Status = ExportStatus.Completed;
        exportRequest.ObjectKey = objectKey;
        exportRequest.CompletedAt = DateTimeOffset.UtcNow;

        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(PresignedExpirySeconds);
        string? privateReportUrl = null;
        string? legacyDownloadUrl = null;
        if (isV3Report)
        {
            privateReportUrl = $"{config["Frontend:PublicUrl"] ?? "http://localhost:3000"}/app/laporan/perkembangan";
            notifier.CreateNotification(exportRequest.RequestedByUserId, NotificationType.ExportReady, new { exportRequest.Id, reportUrl = privateReportUrl });
        }
        else
        {
            legacyDownloadUrl = await minio.PresignedGetObjectAsync(new PresignedGetObjectArgs()
                .WithBucket(bucket)
                .WithObject(objectKey)
                .WithExpiry(PresignedExpirySeconds));
            notifier.CreateNotification(exportRequest.RequestedByUserId, NotificationType.ExportReady, new { exportRequest.Id, downloadUrl = legacyDownloadUrl, expiresAt });
        }

        var requester = await db.Users.AsNoTracking().Where(u => u.Id == exportRequest.RequestedByUserId)
            .Select(u => new { u.Email, u.FullName }).FirstOrDefaultAsync(ct);
        if (requester is not null && !string.IsNullOrWhiteSpace(requester.Email))
        {
            var (subject, html, text) = isV3Report
                ? EmailTemplateRenderer.ExportReadyPrivateReport(requester.FullName, privateReportUrl!)
                : EmailTemplateRenderer.ExportReady(requester.FullName, legacyDownloadUrl!, expiresAt);
            await emailSender.SendAsync(new EmailMessage(requester.Email, "ExportReady", subject, html, text, messageId), ct);
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("{Consumer}: export {Id} ({Format}) selesai, {RowCount} baris -> {ObjectKey}.", Name, exportRequest.Id, exportRequest.Format, rowCount, objectKey);
    }

    private sealed record GeneratedExport(byte[] FileBytes, string Extension, string ContentType, int RowCount);

    private async Task<GeneratedExport> BuildLearningRecordExportAsync(
        ExportRequest exportRequest,
        Guid tenantId,
        CancellationToken ct)
    {
        LearningRecordReportExportSpec? spec;
        try
        {
            spec = JsonSerializer.Deserialize<LearningRecordReportExportSpec>(exportRequest.ReportQueryJson ?? "{}");
        }
        catch (JsonException)
        {
            spec = null;
        }

        if (spec is null || spec.Query.TenantId != tenantId ||
            spec.Scope.ToString() != exportRequest.ExportScope || spec.Quantity != exportRequest.ExportQuantity)
        {
            throw new InvalidOperationException($"V3 export {exportRequest.Id} memiliki snapshot query yang tidak valid.");
        }

        var report = await new LearningRecordQueryService(db).ExecuteReportExportAsync(spec, ct);
        if (exportRequest.Format == ExportFormat.Pdf)
        {
            var rows = report.Items.Select(item => new LearningRecordReportPdfRow(
                item.StudentName,
                item.CompanyName,
                item.PeriodName,
                item.MiddleStatus?.ToString() ?? "Belum dimulai",
                item.FinalStatus?.ToString() ?? "Belum dimulai",
                item.MonitoringStatus?.ToString() ?? "Belum dicatat",
                CompletionStatus(item))).ToList();
            return new GeneratedExport(
                new LearningRecordReportPdfDocument("Laporan Perkembangan PKL", rows).GeneratePdf(),
                "pdf",
                "application/pdf",
                rows.Count);
        }

        var headers = new[] { "Siswa", "DUDI", "Periode", "Middle", "Final", "Monitoring", "Status" };
        var xlsxRows = report.Items.Select(item => (IReadOnlyList<object?>)new object?[]
        {
            item.StudentName,
            item.CompanyName,
            item.PeriodName,
            item.MiddleStatus?.ToString() ?? "Belum dimulai",
            item.FinalStatus?.ToString() ?? "Belum dimulai",
            item.MonitoringStatus?.ToString() ?? "Belum dicatat",
            CompletionStatus(item),
        }).ToList();
        return new GeneratedExport(
            MinimalXlsxWriter.WriteSingleSheet(headers, xlsxRows),
            "xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            xlsxRows.Count);
    }

    private static string CompletionStatus(LearningRecordReportItem item)
    {
        if (item.FinalStatus == LearningAssessmentStatus.Finalized) return "Selesai";
        if (item.MiddleStatus == LearningAssessmentStatus.Reopened || item.FinalStatus == LearningAssessmentStatus.Reopened) return "Sedang diperbaiki";
        if (item.MiddleStatus.HasValue || item.FinalStatus.HasValue) return "Sedang berjalan";
        return "Belum dimulai";
    }

    private sealed record RecapRow(string StudentName, string CompanyName, decimal? MentorAvg, decimal? TeacherAvg, decimal? FinalScore, string Status);

    /// <summary>DUPLIKASI SENGAJA dari GradeRecapEndpoints.GetGradeRecap (Vokasia.Api) - lihat doc-comment kelas.</summary>
    private async Task<List<RecapRow>> BuildRecapRowsAsync(Guid tenantId, Guid periodId, CancellationToken ct)
    {
        var rows = await (
            from p in db.Placements.AsNoTracking()
            where p.TenantId == tenantId && p.PeriodId == periodId
            join s in db.Students.AsNoTracking() on p.StudentId equals s.Id
            join c in db.Companies.AsNoTracking() on p.CompanyId equals c.Id
            join a in db.Assessments.AsNoTracking() on p.Id equals a.PlacementId into aj
            from a in aj.DefaultIfEmpty()
            select new
            {
                StudentName = s.FullName,
                CompanyName = c.Name,
                AssessmentId = (Guid?)a.Id,
                a.FinalScore,
                IsFinal = (bool?)a.IsFinal,
            }).ToListAsync(ct);

        var assessmentIds = rows.Where(r => r.AssessmentId.HasValue).Select(r => r.AssessmentId!.Value).ToList();
        var averages = assessmentIds.Count == 0
            ? []
            : await db.AssessmentScores.AsNoTracking()
                .Where(sc => assessmentIds.Contains(sc.AssessmentId))
                .GroupBy(sc => new { sc.AssessmentId, sc.ScoredBy })
                .Select(g => new { g.Key.AssessmentId, g.Key.ScoredBy, Avg = g.Average(x => x.Value) })
                .ToListAsync(ct);

        var mentorAvgByAssessment = averages.Where(a => a.ScoredBy == ScoredBy.Mentor).ToDictionary(a => a.AssessmentId, a => a.Avg);
        var teacherAvgByAssessment = averages.Where(a => a.ScoredBy == ScoredBy.Teacher).ToDictionary(a => a.AssessmentId, a => a.Avg);

        return rows.Select(r =>
        {
            decimal? mentorAvg = r.AssessmentId.HasValue && mentorAvgByAssessment.TryGetValue(r.AssessmentId.Value, out var m) ? m : null;
            decimal? teacherAvg = r.AssessmentId.HasValue && teacherAvgByAssessment.TryGetValue(r.AssessmentId.Value, out var t) ? t : null;
            var status = r.IsFinal == true ? "Final" : r.AssessmentId.HasValue ? "Draft" : "BelumDinilai";
            return new RecapRow(r.StudentName, r.CompanyName, mentorAvg, teacherAvg, r.FinalScore, status);
        }).ToList();
    }
}
