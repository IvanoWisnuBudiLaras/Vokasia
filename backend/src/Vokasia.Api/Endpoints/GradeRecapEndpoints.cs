using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vokasia.Api.Auth;
using Vokasia.Api.Validation;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Api.Endpoints;

/// <summary>
/// VOK-H5-E1 §4 — GetGradeRecap (tabel rekap per periode, proyeksi TANPA N+1: 2 query total
/// TERLEPAS jumlah placement - lihat doc-comment GetGradeRecap) + RequestExport (202 pola async,
/// FR-ASM-06). File nyata dibangun <see cref="Vokasia.Worker.Consumers.ExportRequestedConsumer"/>.
/// </summary>
public static class GradeRecapEndpoints
{
    public static IEndpointRouteBuilder MapGradeRecapEndpoints(this IEndpointRouteBuilder app)
    {
        var periods = app.MapGroup("/api/periods/{periodId:guid}").WithTags("GradeRecap").AddEndpointFilter<ValidationFilter>();
        periods.MapGet("/grade-recap", GetGradeRecap).RequireAuthorization(RbacPolicies.TenantMember);
        periods.MapPost("/exports", RequestExport).RequireAuthorization(RbacPolicies.DeptHeadPlus);

        return app;
    }

    /// <summary>
    /// 2 query TOTAL (bukan per-baris): (1) placement+student+company+assessment (LEFT JOIN,
    /// proyeksi), (2) agregat AssessmentScores GroupBy(AssessmentId,ScoredBy) utk SEMUA assessment
    /// dari query (1) sekaligus, digabung di memori jadi dictionary. Sama filosofi dgn ListJournals
    /// (JournalEndpoints) - "tanpa N+1" dibuktikan lewat jumlah query KONSTAN, bukan cuma diasumsikan.
    /// </summary>
    private static async Task<IResult> GetGradeRecap(Guid periodId, VokasiaDbContext db, CancellationToken ct)
    {
        var rows = await (
            from p in db.Placements.AsNoTracking()
            where p.PeriodId == periodId
            join s in db.Students.AsNoTracking() on p.StudentId equals s.Id
            join c in db.Companies.AsNoTracking() on p.CompanyId equals c.Id
            join a in db.Assessments.AsNoTracking() on p.Id equals a.PlacementId into aj
            from a in aj.DefaultIfEmpty()
            select new
            {
                PlacementId = p.Id,
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

        var result = rows.Select(r =>
        {
            decimal? mentorAvg = r.AssessmentId.HasValue && mentorAvgByAssessment.TryGetValue(r.AssessmentId.Value, out var m) ? m : null;
            decimal? teacherAvg = r.AssessmentId.HasValue && teacherAvgByAssessment.TryGetValue(r.AssessmentId.Value, out var t) ? t : null;
            var status = r.IsFinal == true ? "Final" : r.AssessmentId.HasValue ? "Draft" : "BelumDinilai";
            return new RecapRowDto(r.PlacementId, r.StudentName, r.CompanyName, mentorAvg, teacherAvg, r.FinalScore, status);
        }).ToList();

        return Results.Ok(result);
    }

    private static async Task<IResult> RequestExport(Guid periodId, RequestExportRequest req, VokasiaDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        if (!tenant.TenantId.HasValue || !tenant.UserId.HasValue)
        {
            return Results.Forbid();
        }

        var periodExists = await db.Periods.AnyAsync(p => p.Id == periodId, ct);
        if (!periodExists)
        {
            return Results.NotFound();
        }

        var exportRequest = new ExportRequest
        {
            Id = Guid.NewGuid(), TenantId = tenant.TenantId.Value, PeriodId = periodId,
            RequestedByUserId = tenant.UserId.Value, Format = req.Format, Status = ExportStatus.Requested,
        };
        db.ExportRequests.Add(exportRequest);

        db.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = "ExportRequested",
            PayloadJson = JsonSerializer.Serialize(new { Id = exportRequest.Id, exportRequest.PeriodId, exportRequest.TenantId, exportRequest.RequestedByUserId, Format = exportRequest.Format.ToString() }),
        });

        await db.SaveChangesAsync(ct);

        return Results.Accepted($"/api/exports/{exportRequest.Id}", new ExportAcceptedDto(exportRequest.Id));
    }
}
