using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Tests.Integration;

/// <summary>
/// VOK-H5-E3 §1 AssessmentFinalizeTests — isi 2 sisi (mentor: Teknis+Kehadiran; guru: Softskill)
/// lewat HTTP sungguhan -&gt; FinalizeAssessment -&gt; FinalScore PRESISI vs 3 kasus hitung manual
/// (kasus SAMA PERSIS dgn Assessment/AssessmentScoringTests.cs - unit murni sudah cover fungsi
/// ComputeWeightedScore; di sini membuktikan pipeline PENUH: submit HTTP dua sisi -> Postgres
/// sungguhan -> finalize -> angka akhir identik). Belum lengkap -> 422 + daftar aspek kurang.
/// </summary>
[Collection("IntegrationTests")]
public class AssessmentFinalizeTests
{
    private readonly VokasiaIntegrationFactory _factory;
    public AssessmentFinalizeTests(VokasiaIntegrationFactory factory) => _factory = factory;

    private sealed record Fixture(Guid TenantId, Guid PeriodId, Guid PlacementId, List<RubricAspect> Aspects);

    private async Task<Fixture> SeedRubricAndPlacementAsync(Guid tenantId, List<RubricAspect> aspects, Guid mentorUserId, Guid teacherUserId)
    {
        using var scope = _factory.CreateDbScope();
        var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();

        var rubric = new RubricTemplate { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Rubrik Finalize", IsDefault = true, Aspects = aspects };
        foreach (var a in aspects) a.RubricTemplateId = rubric.Id;

        var period = new Period { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Periode Finalize", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 12, 31), ClassLevels = "XII", Status = PeriodStatus.Assessment };
        var company = new Company { Id = Guid.NewGuid(), Name = "PT Finalize" };
        var student = new Student { Id = Guid.NewGuid(), TenantId = tenantId, FullName = "Siswa Finalize", MajorId = Guid.NewGuid(), Classroom = "XII A" };
        var placement = new Placement { Id = Guid.NewGuid(), TenantId = tenantId, StudentId = student.Id, CompanyId = company.Id, PeriodId = period.Id, TeacherId = teacherUserId, MentorUserId = mentorUserId, Status = PlacementStatus.Active };

        db.RubricTemplates.Add(rubric);
        db.Periods.Add(period);
        db.Companies.Add(company);
        db.Students.Add(student);
        db.Placements.Add(placement);
        await db.SaveChangesAsync();

        return new Fixture(tenantId, period.Id, placement.Id, aspects);
    }

    private static RubricAspect Aspect(string name, RubricAspectKind kind, int weight) =>
        new() { Id = Guid.NewGuid(), Name = name, Kind = kind, Weight = weight };

    private async Task<decimal?> FinalizeAndGetScoreAsync(Guid periodId, Guid placementId)
    {
        var (_, adminClient) = await _factory.LoginAsAsync(UserRole.TenantAdmin, await TenantIdOfPlacementAsync(placementId), "finalize-admin-" + Guid.NewGuid().ToString("N")[..8]);
        var resp = await adminClient.PostAsJsonAsync($"/api/periods/{periodId}/assessments/finalize", new { PeriodId = periodId, PlacementId = (Guid?)placementId });
        resp.EnsureSuccessStatusCode();

        using var scope = _factory.CreateDbScope();
        var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
        var assessment = await db.Assessments.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.PlacementId == placementId);
        return assessment?.FinalScore;
    }

    private async Task<Guid> TenantIdOfPlacementAsync(Guid placementId)
    {
        using var scope = _factory.CreateDbScope();
        var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
        return await db.Placements.IgnoreQueryFilters().Where(p => p.Id == placementId).Select(p => p.TenantId).FirstAsync();
    }

    [Fact]
    public async Task TwoAspectsEvenSplit_MentorAndTeacherSubmit_FinalizesTo85()
    {
        // Kasus manual #1 (mirror AssessmentScoringTests): 50/50, nilai 80 & 90 -> 85.00
        var tenant = await _factory.SeedTenantAsync();
        var teknis = Aspect("Teknis", RubricAspectKind.Teknis, 50);
        var softskill = Aspect("Softskill", RubricAspectKind.Softskill, 50);
        var (mentor, mentorClient) = await _factory.LoginAsAsync(UserRole.IndustryMentor, null, "finalize-mentor-1");
        var (teacher, teacherClient) = await _factory.LoginAsAsync(UserRole.Teacher, tenant.Id, "finalize-teacher-1");
        var fx = await SeedRubricAndPlacementAsync(tenant.Id, [teknis, softskill], mentor.Id, teacher.Id);

        (await mentorClient.PostAsJsonAsync($"/api/placements/{fx.PlacementId}/assessment/mentor-scores", new object[] { new { AspectId = teknis.Id, Value = 80m } })).EnsureSuccessStatusCode();
        (await teacherClient.PostAsJsonAsync($"/api/placements/{fx.PlacementId}/assessment/teacher-scores", new object[] { new { AspectId = softskill.Id, Value = 90m } })).EnsureSuccessStatusCode();

        var finalScore = await FinalizeAndGetScoreAsync(fx.PeriodId, fx.PlacementId);
        Assert.Equal(85.00m, finalScore);
    }

    [Fact]
    public async Task ThreeAspectsRealisticRubric_MentorAndTeacherSubmit_FinalizesTo85()
    {
        // Kasus manual #2: 40/40/20 (Teknis+Kehadiran mentor, Softskill guru), 85/90/75 -> 85.00
        var tenant = await _factory.SeedTenantAsync();
        var teknis = Aspect("Teknis", RubricAspectKind.Teknis, 40);
        var softskill = Aspect("Softskill", RubricAspectKind.Softskill, 40);
        var kehadiran = Aspect("Kehadiran", RubricAspectKind.Kehadiran, 20);
        var (mentor, mentorClient) = await _factory.LoginAsAsync(UserRole.IndustryMentor, null, "finalize-mentor-2");
        var (teacher, teacherClient) = await _factory.LoginAsAsync(UserRole.Teacher, tenant.Id, "finalize-teacher-2");
        var fx = await SeedRubricAndPlacementAsync(tenant.Id, [teknis, softskill, kehadiran], mentor.Id, teacher.Id);

        (await mentorClient.PostAsJsonAsync($"/api/placements/{fx.PlacementId}/assessment/mentor-scores", new object[]
        {
            new { AspectId = teknis.Id, Value = 85m },
            new { AspectId = kehadiran.Id, Value = 75m },
        })).EnsureSuccessStatusCode();
        (await teacherClient.PostAsJsonAsync($"/api/placements/{fx.PlacementId}/assessment/teacher-scores", new object[] { new { AspectId = softskill.Id, Value = 90m } })).EnsureSuccessStatusCode();

        var finalScore = await FinalizeAndGetScoreAsync(fx.PeriodId, fx.PlacementId);
        Assert.Equal(85.00m, finalScore);
    }

    [Fact]
    public async Task SingleAspectMidpoint_RoundsAwayFromZero_FinalizesTo87Point13()
    {
        // Kasus manual #3: weight=100, value=87.125 -> 87.13 (AwayFromZero, bukan ToEven/banker's).
        var tenant = await _factory.SeedTenantAsync();
        var teknis = Aspect("Tunggal", RubricAspectKind.Teknis, 100);
        var (mentor, mentorClient) = await _factory.LoginAsAsync(UserRole.IndustryMentor, null, "finalize-mentor-3");
        var fx = await SeedRubricAndPlacementAsync(tenant.Id, [teknis], mentor.Id, Guid.NewGuid());

        (await mentorClient.PostAsJsonAsync($"/api/placements/{fx.PlacementId}/assessment/mentor-scores", new object[] { new { AspectId = teknis.Id, Value = 87.125m } })).EnsureSuccessStatusCode();

        var finalScore = await FinalizeAndGetScoreAsync(fx.PeriodId, fx.PlacementId);
        Assert.Equal(87.13m, finalScore);
    }

    [Fact]
    public async Task IncompleteScores_FinalizeSinglePlacement_Returns422WithMissingAspectNames()
    {
        var tenant = await _factory.SeedTenantAsync();
        var teknis = Aspect("Teknis", RubricAspectKind.Teknis, 60);
        var softskill = Aspect("Softskill", RubricAspectKind.Softskill, 40);
        var (mentor, mentorClient) = await _factory.LoginAsAsync(UserRole.IndustryMentor, null, "finalize-mentor-incomplete");
        var fx = await SeedRubricAndPlacementAsync(tenant.Id, [teknis, softskill], mentor.Id, Guid.NewGuid());

        // Softskill (sisi guru) SENGAJA tidak diisi.
        (await mentorClient.PostAsJsonAsync($"/api/placements/{fx.PlacementId}/assessment/mentor-scores", new object[] { new { AspectId = teknis.Id, Value = 80m } })).EnsureSuccessStatusCode();

        var (_, adminClient) = await _factory.LoginAsAsync(UserRole.TenantAdmin, tenant.Id, "finalize-admin-incomplete");
        var resp = await adminClient.PostAsJsonAsync($"/api/periods/{fx.PeriodId}/assessments/finalize", new { PeriodId = fx.PeriodId, PlacementId = (Guid?)fx.PlacementId });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var incomplete = body.GetProperty("incomplete").EnumerateArray().ToList();
        Assert.Single(incomplete);
        var missing = incomplete[0].GetProperty("missingAspectNames").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains("Softskill", missing);
    }
}
