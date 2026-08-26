using Vokasia.Api.Auth;
using Vokasia.Api.Authorization;
using Vokasia.Domain.Common;
using Vokasia.Infrastructure.Queries;

namespace Vokasia.Api.Endpoints;

public sealed class LearningRecordReportQueryParameters
{
    public Guid? PeriodId { get; set; }
    public Guid? CompanyId { get; set; }
    public string? Stage { get; set; }
    public string? Status { get; set; }
    public string? MonitoringStatus { get; set; }
    public string? Search { get; set; }
    public string? Sort { get; set; }
    public string? Direction { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public sealed record LearningRecordReportRowDto(
    Guid PlacementId,
    string StudentName,
    Guid CompanyId,
    string CompanyName,
    Guid PeriodId,
    string PeriodName,
    DateOnly PeriodStartDate,
    DateOnly PeriodEndDate,
    string? MiddleStatus,
    string? FinalStatus,
    string? MonitoringStatus,
    DateTimeOffset? MonitoringUpdatedAt,
    string CompletionStatus);

public sealed record LearningRecordReportSummaryDto(
    int TotalCount,
    int CompleteCount,
    int IncompleteCount,
    int NeedsAttentionCount);

public sealed record LearningRecordReportFindingDto(string Kind, int Count, string Label);

public sealed record LearningRecordReportResponseDto(
    List<LearningRecordReportRowDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    LearningRecordReportSummaryDto Summary,
    List<LearningRecordReportFindingDto> Findings);

public static class LearningRecordReportingEndpoints
{
    public static IEndpointRouteBuilder MapLearningRecordReportingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/teacher/learning-record/report", ListReport)
            .WithTags("Learning Record")
            .RequireAuthorization(RbacPolicies.TeacherPlus);
        return app;
    }

    private static async Task<IResult> ListReport(
        HttpRequest http,
        ITenantContext tenant,
        LearningRecordQueryService queries,
        CancellationToken ct)
    {
        if (!tenant.TenantId.HasValue || !tenant.UserId.HasValue)
        {
            return Results.Forbid();
        }

        var httpQuery = http.Query;
        if (!TryParseOptionalGuid(httpQuery["periodId"].ToString(), out var periodId) ||
            !TryParseOptionalGuid(httpQuery["companyId"].ToString(), out var companyId) ||
            !TryParseInt(httpQuery["page"].ToString(), 1, out var page) ||
            !TryParseInt(httpQuery["pageSize"].ToString(), 50, out var pageSize))
        {
            return Results.UnprocessableEntity(new { message = "Parameter laporan tidak valid." });
        }

        var request = new LearningRecordReportQueryParameters
        {
            PeriodId = periodId,
            CompanyId = companyId,
            Stage = httpQuery["stage"].ToString(),
            Status = httpQuery["status"].ToString(),
            MonitoringStatus = httpQuery["monitoringStatus"].ToString(),
            Search = httpQuery["search"].ToString(),
            Sort = httpQuery["sort"].ToString(),
            Direction = httpQuery["direction"].ToString(),
            Page = page,
            PageSize = pageSize,
        };

        if (request.Page < 1 || request.Page > 1_000_000)
        {
            return Results.UnprocessableEntity(new { message = "Nomor halaman tidak valid." });
        }

        if (request.PageSize is not (25 or 50 or 100))
        {
            return Results.UnprocessableEntity(new { message = "Ukuran halaman harus 25, 50, atau 100." });
        }

        if (request.Search?.Trim().Length > 100)
        {
            return Results.UnprocessableEntity(new { message = "Pencarian maksimal 100 karakter." });
        }

        if (!TryParseEnum(request.Stage, out LearningAssessmentStage? stage) ||
            !TryParseEnum(request.Status, out LearningAssessmentStatus? status) ||
            !TryParseEnum(request.MonitoringStatus, out LearningRecordMonitoringStatus? monitoringStatus))
        {
            return Results.UnprocessableEntity(new { message = "Filter laporan tidak dikenal." });
        }

        var sort = request.Sort?.Trim().ToLowerInvariant() switch
        {
            null or "" or "student" or "studentname" => LearningRecordReportSort.StudentName,
            "company" or "companyname" => LearningRecordReportSort.CompanyName,
            "period" or "periodname" => LearningRecordReportSort.PeriodName,
            "monitoring" or "monitoringupdatedat" => LearningRecordReportSort.MonitoringUpdatedAt,
            _ => (LearningRecordReportSort?)null,
        };
        if (sort is null)
        {
            return Results.UnprocessableEntity(new { message = "Urutan laporan tidak dikenal." });
        }

        var direction = request.Direction?.Trim().ToLowerInvariant();
        if (direction is not (null or "" or "asc" or "desc"))
        {
            return Results.UnprocessableEntity(new { message = "Arah urutan laporan tidak dikenal." });
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
        var report = await queries.ExecuteReportAsync(query, ct);
        var totalPages = report.TotalCount == 0 ? 0 : (int)Math.Ceiling(report.TotalCount / (double)request.PageSize);
        var findings = new List<LearningRecordReportFindingDto>();
        if (report.Summary.NeedsAttentionCount > 0)
        {
            findings.Add(new("Monitoring", report.Summary.NeedsAttentionCount, "Perlu perhatian dari Guru"));
        }
        if (report.Summary.IncompleteCount > 0)
        {
            findings.Add(new("Assessment", report.Summary.IncompleteCount, "Penilaian belum selesai"));
        }

        return Results.Ok(new LearningRecordReportResponseDto(
            report.Items.Select(ToDto).ToList(),
            request.Page,
            request.PageSize,
            report.TotalCount,
            totalPages,
            new LearningRecordReportSummaryDto(
                report.Summary.TotalCount,
                report.Summary.CompleteCount,
                report.Summary.IncompleteCount,
                report.Summary.NeedsAttentionCount),
            findings));
    }

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

    private static bool TryParseOptionalGuid(string raw, out Guid? value)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            value = null;
            return true;
        }

        if (Guid.TryParse(raw, out var parsed))
        {
            value = parsed;
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryParseInt(string raw, int fallback, out int value)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            value = fallback;
            return true;
        }

        return int.TryParse(raw, out value);
    }

    private static LearningRecordReportRowDto ToDto(LearningRecordReportItem item) => new(
        item.PlacementId,
        item.StudentName,
        item.CompanyId,
        item.CompanyName,
        item.PeriodId,
        item.PeriodName,
        item.PeriodStartDate,
        item.PeriodEndDate,
        item.MiddleStatus?.ToString(),
        item.FinalStatus?.ToString(),
        item.MonitoringStatus?.ToString(),
        item.MonitoringUpdatedAt,
        CompletionStatus(item));

    private static string CompletionStatus(LearningRecordReportItem item)
    {
        if (item.FinalStatus == LearningAssessmentStatus.Finalized)
        {
            return "Finalized";
        }

        if (item.MiddleStatus == LearningAssessmentStatus.Reopened || item.FinalStatus == LearningAssessmentStatus.Reopened)
        {
            return "CorrectionInProgress";
        }

        if (item.MiddleStatus.HasValue || item.FinalStatus.HasValue)
        {
            return "InProgress";
        }

        return "NotStarted";
    }
}
