using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vokasia.Domain.Common;
using Vokasia.Infrastructure.Identity;
using Vokasia.Infrastructure.Persistence;
using Vokasia.Infrastructure.Seeding;

namespace Vokasia.Tests.Integration;

[Collection("IntegrationTests")]
public sealed class DemoCertificateSeedTests
{
    private readonly VokasiaIntegrationFactory _factory;
    public DemoCertificateSeedTests(VokasiaIntegrationFactory factory) => _factory = factory;

    [Fact]
    public async Task DemoCertificateScenario_HasNormalCertificatePrerequisitesAndRequest()
    {
        using (var scope = _factory.CreateDbScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            await DemoSeeder.SeedDemoDataAsync(db, users, new SeedOptions(3, 100, 300, 90), forceReset: true);
        }

        using var verifyScope = _factory.CreateDbScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
        var student = await verifyDb.Students.AsNoTracking().SingleAsync(s => s.FullName == "DEMO-CERTIFICATE");
        Assert.NotNull(student.UserId);
        Assert.NotEqual(Guid.Empty, student.UserId.Value);
        Assert.NotNull(await verifyDb.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == student.UserId.Value && u.Role == UserRole.Student));

        var placement = await verifyDb.Placements.AsNoTracking().SingleAsync(p => p.StudentId == student.Id);
        Assert.Equal(PlacementStatus.Completed, placement.Status);
        var period = await verifyDb.Periods.AsNoTracking().SingleAsync(p => p.Id == placement.PeriodId);
        Assert.Equal(PeriodStatus.Assessment, period.Status);
        var assessment = await verifyDb.Assessments.AsNoTracking().SingleAsync(a => a.PlacementId == placement.Id);
        Assert.True(assessment.IsFinal);
        Assert.Equal(88m, assessment.FinalScore);
        var scores = await verifyDb.AssessmentScores.AsNoTracking().Where(s => s.AssessmentId == assessment.Id).ToListAsync();
        Assert.Equal(3, scores.Count);
        Assert.Contains(scores, s => s.ScoredBy == ScoredBy.Teacher);
        Assert.Contains(scores, s => s.ScoredBy == ScoredBy.Mentor);

        var request = await verifyDb.OutboxMessages.AsNoTracking().SingleAsync(o => o.Type == "CertificateRequested" && o.PayloadJson.Contains(placement.Id.ToString()));
        var payload = JsonSerializer.Deserialize<JsonElement>(request.PayloadJson);
        Assert.Equal(placement.Id, payload.GetProperty("PlacementId").GetGuid());
        Assert.Equal(placement.TenantId, payload.GetProperty("TenantId").GetGuid());

        var resolvable = await (
            from p in verifyDb.Placements.AsNoTracking()
            join s in verifyDb.Students.AsNoTracking() on p.StudentId equals s.Id
            join c in verifyDb.Companies.AsNoTracking() on p.CompanyId equals c.Id
            join per in verifyDb.Periods.AsNoTracking() on p.PeriodId equals per.Id
            join t in verifyDb.Tenants.AsNoTracking() on p.TenantId equals t.Id
            join a in verifyDb.Assessments.AsNoTracking() on p.Id equals a.PlacementId
            where p.Id == placement.Id && a.IsFinal
            select new { s.FullName, c.Name, per.StartDate, per.EndDate, t.SchoolName, a.FinalScore }).SingleAsync();
        Assert.Equal("DEMO-CERTIFICATE", resolvable.FullName);
        Assert.Equal(88m, resolvable.FinalScore);
    }

    [Fact]
    public async Task DemoCertificateSeed_IsIdempotentAfterMarkerExists()
    {
        using var scope = _factory.CreateDbScope();
        var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var before = await db.OutboxMessages.CountAsync(o => o.Type == "CertificateRequested");
        var result = await DemoSeeder.SeedDemoDataAsync(db, users, new SeedOptions(3, 100, 300, 90), forceReset: false);
        var after = await db.OutboxMessages.CountAsync(o => o.Type == "CertificateRequested");
        Assert.StartsWith("SKIP:", result, StringComparison.Ordinal);
        Assert.Equal(before, after);
    }
}
