using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Minio;
using Vokasia.Api.Auth;
using Vokasia.Api.Authorization;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Persistence;
using Vokasia.Infrastructure.Queries;

namespace Vokasia.Api.Endpoints;

public sealed record LearningRecordReportExportRequest(
    string? Format,
    string? Scope,
    int? Quantity,
    int Page = 1,
    int PageSize = 50,
    Guid? PeriodId = null,
    Guid? CompanyId = null,
    string? Stage = null,
    string? Status = null,
    string? MonitoringStatus = null,
    string? Search = null,
    string? Sort = null,
    string? Direction = null);

public static class LearningRecordReportExportEndpoints
{
    private const string V3ReportKind = "V3LearningRecord";

    public static IEndpointRouteBuilder MapLearningRecordReportExportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/teacher/learning-record/report")
            .WithTags("Learning Record")
            .RequireAuthorization(RbacPolicies.TeacherPlus);

        group.MapPost("/export", RequestExport);
        group.MapGet("/export/{exportId:guid}", GetExport);
        group.MapGet("/export/{exportId:guid}/download", DownloadExport);
        return app;
    }

    private static async Task<IResult> RequestExport(
        LearningRecordReportExportRequest? request,
        ITenantContext tenant,
        VokasiaDbContext db,
        CancellationToken ct)
    {
        if (!tenant.TenantId.HasValue || !tenant.UserId.HasValue || request is null)
        {
            return Results.Forbid();
        }

        if (!TryParseEnum(request.Format, out ExportFormat? format) ||
            !TryParseEnum(request.Scope, out LearningRecordReportExportScope? scope) ||
            !TryParseEnum(request.Stage, out LearningAssessmentStage? stage) ||
            !TryParseEnum(request.Status, out LearningAssessmentStatus? status) ||
            !TryParseEnum(request.MonitoringStatus, out LearningRecordMonitoringStatus? monitoringStatus))
        {
            return Results.UnprocessableEntity(new { message = "Konfigurasi export tidak dikenal." });
        }

        if (format is null || scope is null || !IsValidQuantity(request.Quantity) ||
            request.Page is < 1 or > 1_000_000 || request.PageSize is not (25 or 50 or 100) ||
            request.Search?.Trim().Length > 100)
        {
            return Results.UnprocessableEntity(new { message = "Konfigurasi export tidak valid." });
        }

        if (format == ExportFormat.Pdf && request.Quantity is null)
        {
            return Results.UnprocessableEntity(new { message = "PDF harus memakai jumlah baris yang dibatasi." });
        }

        var sort = request.Sort?.Trim().ToLowerInvariant() switch
        {
            null or "" or "student" or "studentname" => LearningRecordReportSort.StudentName,
            "company" or "companyname" => LearningRecordReportSort.CompanyName,
            "period" or "periodname" => LearningRecordReportSort.PeriodName,
            "monitoring" or "monitoringupdatedat" => LearningRecordReportSort.MonitoringUpdatedAt,
            _ => (LearningRecordReportSort?)null,
        };
        var direction = request.Direction?.Trim().ToLowerInvariant();
        if (sort is null || direction is not (null or "" or "asc" or "desc"))
        {
            return Results.UnprocessableEntity(new { message = "Urutan export tidak valid." });
        }

        var query = new LearningRecordReportQuery(
            tenant.TenantId.Value,
            tenant.Role == nameof(UserRole.Teacher) ? tenant.UserId : null,
            request.PeriodId,
            request.CompanyId,
            stage,
            status,
            monitoringStatus,
            request.Search?.Trim(),
            sort.Value,
            direction == "desc",
            request.Page,
            request.PageSize);
        var exportScope = scope.Value;
        var quantity = exportScope == LearningRecordReportExportScope.CurrentPage
            ? request.PageSize
            : request.Quantity;
        if (format == ExportFormat.Pdf && quantity is null)
        {
            return Results.UnprocessableEntity(new { message = "PDF harus memakai jumlah baris yang dibatasi." });
        }

        var exportRequest = new ExportRequest
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.TenantId.Value,
            PeriodId = request.PeriodId ?? Guid.Empty,
            RequestedByUserId = tenant.UserId.Value,
            Format = format.Value,
            Status = ExportStatus.Requested,
            ReportKind = V3ReportKind,
            ReportQueryJson = JsonSerializer.Serialize(new LearningRecordReportExportSpec(query, exportScope, quantity)),
            ExportScope = exportScope.ToString(),
            ExportQuantity = quantity,
        };
        db.ExportRequests.Add(exportRequest);
        db.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = "ExportRequested",
            PayloadJson = JsonSerializer.Serialize(new
            {
                Id = exportRequest.Id,
                PeriodId = exportRequest.PeriodId,
                TenantId = exportRequest.TenantId,
                RequestedByUserId = exportRequest.RequestedByUserId,
                Format = exportRequest.Format.ToString(),
            }),
        });
        await db.SaveChangesAsync(ct);

        return Results.Accepted($"/api/teacher/learning-record/report/export/{exportRequest.Id}",
            new { exportId = exportRequest.Id, status = exportRequest.Status.ToString() });
    }

    private static async Task<IResult> GetExport(
        Guid exportId,
        ITenantContext tenant,
        VokasiaDbContext db,
        CancellationToken ct)
    {
        if (!tenant.TenantId.HasValue || !tenant.UserId.HasValue)
        {
            return Results.Forbid();
        }

        var exportRequest = await db.ExportRequests.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == exportId &&
                item.ReportKind == V3ReportKind &&
                item.RequestedByUserId == tenant.UserId.Value, ct);
        if (exportRequest is null)
        {
            return Results.NotFound();
        }

        if (exportRequest.Status != ExportStatus.Completed || string.IsNullOrWhiteSpace(exportRequest.ObjectKey))
        {
            return Results.Ok(new { exportId, status = exportRequest.Status.ToString() });
        }

        return Results.Ok(new { exportId, status = exportRequest.Status.ToString() });
    }

    private static async Task<IResult> DownloadExport(
        Guid exportId,
        ITenantContext tenant,
        VokasiaDbContext db,
        IMinioClient minio,
        IConfiguration config,
        CancellationToken ct)
    {
        if (!tenant.TenantId.HasValue || !tenant.UserId.HasValue)
        {
            return Results.Forbid();
        }

        var exportRequest = await db.ExportRequests.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == exportId &&
                item.ReportKind == V3ReportKind &&
                item.RequestedByUserId == tenant.UserId.Value, ct);
        if (exportRequest is null || exportRequest.Status != ExportStatus.Completed ||
            !ObjectStorageKeyPolicy.IsOwnedKey(exportRequest.ObjectKey, tenant.TenantId.Value, "exports"))
        {
            return Results.NotFound();
        }

        var (contentType, extension) = exportRequest.Format == ExportFormat.Pdf
            ? ("application/pdf", "pdf")
            : ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "xlsx");
        try
        {
            await using var file = new MemoryStream();
            await minio.GetObjectAsync(new Minio.DataModel.Args.GetObjectArgs()
                .WithBucket(config["Minio:Bucket"] ?? "vokasia-journal")
                .WithObject(exportRequest.ObjectKey)
                .WithCallbackStream(stream => stream.CopyTo(file)), ct);
            return Results.File(file.ToArray(), contentType, $"learning-record-report.{extension}");
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return Results.NotFound();
        }
    }

    private static bool IsValidQuantity(int? quantity) => quantity is null or 25 or 50 or 100 or 250 or 500;

    private static bool TryParseEnum<TEnum>(string? raw, out TEnum? value)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            value = null;
            return true;
        }

        if (Enum.TryParse<TEnum>(raw, true, out var parsed) && Enum.IsDefined(parsed))
        {
            value = parsed;
            return true;
        }

        value = null;
        return false;
    }
}
