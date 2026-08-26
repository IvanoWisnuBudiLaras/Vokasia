using Microsoft.EntityFrameworkCore;
using Vokasia.Api.Auth;
using Vokasia.Api.Authorization;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Api.Endpoints;

/// <summary>Read-only tenant operations projections used by the Tenant Admin workspace.</summary>
public static class TenantOperationsEndpoints
{
    public static IEndpointRouteBuilder MapTenantOperationsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tenant-operations").WithTags("Tenant Operations");
        group.MapGet("/mentors", ListMentors).RequireAuthorization(RbacPolicies.TenantAdminOnly);
        group.MapGet("/mentors/{mentorId:guid}", GetMentor).RequireAuthorization(RbacPolicies.TenantAdminOnly);
        return app;
    }

    private static async Task<IResult> ListMentors(VokasiaDbContext db, CancellationToken ct)
    {
        var placements = await db.Placements.AsNoTracking()
            .Where(p => p.MentorUserId.HasValue)
            .Select(p => new { p.MentorUserId, p.Id })
            .ToListAsync(ct);
        if (placements.Count == 0) return Results.Ok(Array.Empty<TenantMentorSummaryDto>());

        var mentorIds = placements.Select(p => p.MentorUserId!.Value).Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(u => mentorIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, ct);
        var placementIds = placements.Select(p => p.Id).ToList();
        var pending = await db.JournalEntries.AsNoTracking().Where(e => placementIds.Contains(e.PlacementId) && e.Status == Domain.Common.JournalEntryStatus.Submitted)
            .GroupBy(e => e.PlacementId).Select(g => new { PlacementId = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.PlacementId, x => x.Count, ct);
        var incomplete = await db.Assessments.AsNoTracking().Where(a => placementIds.Contains(a.PlacementId) && !a.IsFinal)
            .Select(a => a.PlacementId).ToListAsync(ct);

        var result = placements.GroupBy(p => p.MentorUserId!.Value).Select(group =>
        {
            users.TryGetValue(group.Key, out var user);
            var ids = group.Select(p => p.Id).ToList();
            return new TenantMentorSummaryDto(group.Key, user?.FullName ?? "Pembimbing industri", user?.Email ?? "", ids.Count,
                ids.Sum(id => pending.GetValueOrDefault(id)), ids.Count(id => incomplete.Contains(id)), user?.IsActive ?? false);
        }).OrderBy(x => x.FullName).ToList();
        return Results.Ok(result);
    }

    private static async Task<IResult> GetMentor(Guid mentorId, VokasiaDbContext db, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().Where(u => u.Id == mentorId).Select(u => new { u.Id, u.FullName, u.Email, u.IsActive }).FirstOrDefaultAsync(ct);
        if (user is null) return Results.NotFound();

        var placements = await (from p in db.Placements.AsNoTracking()
                                join student in db.Students.AsNoTracking() on p.StudentId equals student.Id
                                join company in db.Companies.AsNoTracking() on p.CompanyId equals company.Id
                                where p.MentorUserId == mentorId
                                select new TenantMentorStudentDto(p.Id, student.FullName, company.Name, p.Status, 0, "Belum dimulai"))
            .ToListAsync(ct);
        if (placements.Count == 0) return Results.NotFound();

        var ids = placements.Select(p => p.PlacementId).ToList();
        var pending = await db.JournalEntries.AsNoTracking().Where(e => ids.Contains(e.PlacementId) && e.Status == Domain.Common.JournalEntryStatus.Submitted)
            .GroupBy(e => e.PlacementId).Select(g => new { PlacementId = g.Key, Count = g.Count(), Last = g.Max(x => x.SubmittedAt) }).ToListAsync(ct);
        var assessments = await db.Assessments.AsNoTracking().Where(a => ids.Contains(a.PlacementId)).ToDictionaryAsync(a => a.PlacementId, ct);
        var latest = pending.Count == 0 ? null : pending.Max(x => (DateTimeOffset?)x.Last);
        var result = placements.Select(item =>
        {
            var pendingCount = pending.FirstOrDefault(x => x.PlacementId == item.PlacementId)?.Count ?? 0;
            var assessmentStatus = !assessments.TryGetValue(item.PlacementId, out var assessment) ? "Belum dimulai" : assessment.IsFinal ? "Final" : "Belum final";
            return item with { PendingJournalCount = pendingCount, AssessmentStatus = assessmentStatus };
        }).ToList();
        return Results.Ok(new TenantMentorDetailDto(user.Id, user.FullName, user.Email ?? "", user.IsActive, latest, result));
    }
}
