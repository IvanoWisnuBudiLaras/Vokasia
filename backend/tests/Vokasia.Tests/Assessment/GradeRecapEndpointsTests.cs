using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Persistence;
using Vokasia.Tests.Auth;

namespace Vokasia.Tests.Assessment;

/// <summary>VOK-H5-E1 §4 — GetGradeRecap (proyeksi campuran status) + RequestExport (202 + baris ExportRequest).</summary>
public class GradeRecapEndpointsTests : IClassFixture<VokasiaApiFactory>
{
    private readonly VokasiaApiFactory _factory;
    public GradeRecapEndpointsTests(VokasiaApiFactory factory) => _factory = factory;

    private async Task<HttpClient> AuthClientAsync(UserRole role, Guid tenantId, string emailPrefix)
    {
        var user = await AuthTestHelpers.SeedUserAsync(_factory, emailPrefix, role, tenantId);
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var (accessToken, _) = await AuthTestHelpers.LoginAndExchangeAsync(client, user.Email!);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    [Fact]
    public async Task GetGradeRecap_MixOfAssessmentStatuses_ReturnsCorrectAveragesAndStatusPerRow()
    {
        var tenantId = Guid.NewGuid();
        var client = await AuthClientAsync(UserRole.TenantAdmin, tenantId, "recap-admin");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();

        var period = new Period { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Periode Rekap", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 12, 31), ClassLevels = "XII", Status = PeriodStatus.Active };
        var company = new Company { Id = Guid.NewGuid(), Name = "PT Rekap" };
        var teknis = new RubricAspect { Id = Guid.NewGuid(), Name = "Teknis", Kind = RubricAspectKind.Teknis, Weight = 60 };
        var softskill = new RubricAspect { Id = Guid.NewGuid(), Name = "Softskill", Kind = RubricAspectKind.Softskill, Weight = 40 };
        var rubric = new RubricTemplate { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Rubrik Rekap", IsDefault = true, Aspects = [teknis, softskill] };
        foreach (var a in rubric.Aspects) a.RubricTemplateId = rubric.Id;

        // Placement A: belum dinilai sama sekali (tanpa Assessment row).
        var studentA = new Student { Id = Guid.NewGuid(), TenantId = tenantId, FullName = "Siswa A Belum Dinilai", MajorId = Guid.NewGuid(), Classroom = "XII A" };
        var placementA = new Placement { Id = Guid.NewGuid(), TenantId = tenantId, StudentId = studentA.Id, CompanyId = company.Id, PeriodId = period.Id, TeacherId = Guid.NewGuid(), Status = PlacementStatus.Active };

        // Placement B: draft (mentor sudah isi, belum final).
        var studentB = new Student { Id = Guid.NewGuid(), TenantId = tenantId, FullName = "Siswa B Draft", MajorId = Guid.NewGuid(), Classroom = "XII A" };
        var placementB = new Placement { Id = Guid.NewGuid(), TenantId = tenantId, StudentId = studentB.Id, CompanyId = company.Id, PeriodId = period.Id, TeacherId = Guid.NewGuid(), Status = PlacementStatus.Active };
        var assessmentB = new Vokasia.Domain.Entities.Assessment { Id = Guid.NewGuid(), TenantId = tenantId, PlacementId = placementB.Id, RubricTemplateId = rubric.Id, IsFinal = false };
        var scoreB = new AssessmentScore { Id = Guid.NewGuid(), AssessmentId = assessmentB.Id, RubricAspectId = teknis.Id, ScoredBy = ScoredBy.Mentor, ScoredByUserId = Guid.NewGuid(), Value = 80m };

        // Placement C: sudah final.
        var studentC = new Student { Id = Guid.NewGuid(), TenantId = tenantId, FullName = "Siswa C Final", MajorId = Guid.NewGuid(), Classroom = "XII B" };
        var placementC = new Placement { Id = Guid.NewGuid(), TenantId = tenantId, StudentId = studentC.Id, CompanyId = company.Id, PeriodId = period.Id, TeacherId = Guid.NewGuid(), Status = PlacementStatus.Active };
        var assessmentC = new Vokasia.Domain.Entities.Assessment { Id = Guid.NewGuid(), TenantId = tenantId, PlacementId = placementC.Id, RubricTemplateId = rubric.Id, IsFinal = true, FinalScore = 84.00m, FinalizedAt = DateTimeOffset.UtcNow };
        var scoreC1 = new AssessmentScore { Id = Guid.NewGuid(), AssessmentId = assessmentC.Id, RubricAspectId = teknis.Id, ScoredBy = ScoredBy.Mentor, ScoredByUserId = Guid.NewGuid(), Value = 90m };
        var scoreC2 = new AssessmentScore { Id = Guid.NewGuid(), AssessmentId = assessmentC.Id, RubricAspectId = softskill.Id, ScoredBy = ScoredBy.Teacher, ScoredByUserId = Guid.NewGuid(), Value = 75m };

        db.Periods.Add(period);
        db.Companies.Add(company);
        db.RubricTemplates.Add(rubric);
        db.Students.AddRange(studentA, studentB, studentC);
        db.Placements.AddRange(placementA, placementB, placementC);
        db.Assessments.AddRange(assessmentB, assessmentC);
        db.AssessmentScores.AddRange(scoreB, scoreC1, scoreC2);
        await db.SaveChangesAsync();

        var resp = await client.GetAsync($"/api/periods/{period.Id}/grade-recap");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, body.GetArrayLength());

        JsonElement RowFor(Guid placementId) => body.EnumerateArray().First(r => r.GetProperty("placementId").GetGuid() == placementId);

        var rowA = RowFor(placementA.Id);
        Assert.Equal("BelumDinilai", rowA.GetProperty("status").GetString());
        Assert.True(rowA.GetProperty("mentorAvg").ValueKind is JsonValueKind.Null);

        var rowB = RowFor(placementB.Id);
        Assert.Equal("Draft", rowB.GetProperty("status").GetString());
        Assert.Equal(80m, rowB.GetProperty("mentorAvg").GetDecimal());
        Assert.True(rowB.GetProperty("teacherAvg").ValueKind is JsonValueKind.Null);

        var rowC = RowFor(placementC.Id);
        Assert.Equal("Final", rowC.GetProperty("status").GetString());
        Assert.Equal(90m, rowC.GetProperty("mentorAvg").GetDecimal());
        Assert.Equal(75m, rowC.GetProperty("teacherAvg").GetDecimal());
        Assert.Equal(84.00m, rowC.GetProperty("finalScore").GetDecimal());
    }

    [Fact]
    public async Task RequestExport_ValidPeriod_Returns202AndPersistsExportRequestRow()
    {
        var tenantId = Guid.NewGuid();
        var client = await AuthClientAsync(UserRole.DeptHead, tenantId, "recap-export-ok");
        var testPeriodId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            db.Periods.Add(new Period { Id = testPeriodId, TenantId = tenantId, Name = "Periode Export", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 12, 31), ClassLevels = "XII", Status = PeriodStatus.Active });
            await db.SaveChangesAsync();
        }

        var resp = await client.PostAsJsonAsync($"/api/periods/{testPeriodId}/exports", new { Format = 1 }); // 1 = ExportFormat.Pdf

        Assert.Equal(HttpStatusCode.Accepted, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var exportId = body.GetProperty("exportId").GetGuid();

        using var verify = _factory.Services.CreateScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<VokasiaDbContext>();
        var row = await verifyDb.ExportRequests.FirstOrDefaultAsync(e => e.Id == exportId);
        Assert.NotNull(row);
        Assert.Equal(ExportStatus.Requested, row!.Status);
        Assert.Equal(ExportFormat.Pdf, row.Format);

        var outboxRow = await verifyDb.OutboxMessages.FirstOrDefaultAsync(o => o.Type == "ExportRequested");
        Assert.NotNull(outboxRow);
    }

    [Fact]
    public async Task RequestExport_PeriodNotFound_Returns404()
    {
        var tenantId = Guid.NewGuid();
        var client = await AuthClientAsync(UserRole.DeptHead, tenantId, "recap-export-404");

        var resp = await client.PostAsJsonAsync($"/api/periods/{Guid.NewGuid()}/exports", new { Format = 0 });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
