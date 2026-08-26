using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vokasia.Api.Auth;
using Vokasia.Api.Authorization;
using Vokasia.Domain.Common;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Api.Endpoints;

/// <summary>
/// Slice 4 reporting detail reads. Rows come from tenant-scoped journals,
/// placements, students, companies, and journal slots only.
/// </summary>
public static class ReportingEndpoints
{
    public static IEndpointRouteBuilder MapReportingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/reports/school/{periodId:guid}/journals", ListJournalReport)
            .WithTags("Reporting")
            .RequireAuthorization(RbacPolicies.TenantMember);

        return app;
    }

    private static async Task<IResult> ListJournalReport(
        Guid periodId,
        [FromQuery] JournalEntryStatus? status,
        VokasiaDbContext db,
        ITenantContext tenant,
        CancellationToken ct)
    {
        var teacherScoped = string.Equals(tenant.Role, UserRole.Teacher.ToString(), StringComparison.OrdinalIgnoreCase);
        if (teacherScoped && !tenant.UserId.HasValue)
        {
            return Results.Forbid();
        }

        var query =
            from entry in db.JournalEntries.AsNoTracking()
            join slot in db.JournalSlots.AsNoTracking() on entry.SlotId equals slot.Id
            join placement in db.Placements.AsNoTracking() on entry.PlacementId equals placement.Id
            join student in db.Students.AsNoTracking() on placement.StudentId equals student.Id
            join company in db.Companies.AsNoTracking() on placement.CompanyId equals company.Id
            where placement.PeriodId == periodId
            select new
            {
                JournalId = entry.Id,
                PlacementId = placement.Id,
                StudentName = student.FullName,
                CompanyName = company.Name,
                Date = slot.Date,
                entry.Status,
                entry.SubmittedAt,
                entry.MentorNote,
                placement.TeacherId,
            };

        if (teacherScoped)
        {
            query = query.Where(row => row.TeacherId == tenant.UserId!.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(row => row.Status == status.Value);
        }

        var rows = await query
            .OrderByDescending(row => row.SubmittedAt)
            .Select(row => new JournalReportRowDto(
                row.JournalId,
                row.PlacementId,
                row.StudentName,
                row.CompanyName,
                row.Date,
                row.Status,
                row.SubmittedAt,
                row.MentorNote))
            .ToListAsync(ct);

        return Results.Ok(rows);
    }
}
