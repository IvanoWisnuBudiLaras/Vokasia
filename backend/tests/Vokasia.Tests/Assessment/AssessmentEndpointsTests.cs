using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Identity;
using Vokasia.Infrastructure.Persistence;
using Vokasia.Tests.Auth;

namespace Vokasia.Tests.Assessment;

/// <summary>
/// VOK-H5-E1 §3 — SubmitMentorScores/SubmitTeacherScores (dua sisi), GetAssessment (satu DTO),
/// FinalizeAssessment (kunci + AC 422 kalau kurang). Rubrik uji: Teknis 40/Softskill 40/Kehadiran
/// 20, nilai 85/90/75 -> FinalScore manual = 85.00 (kasus SAMA PERSIS dgn
/// AssessmentScoringTests.ComputeWeightedScore_ThreeAspectsRealisticRubric, dibuktikan lewat HTTP
/// end-to-end di sini, bukan cuma unit murni).
/// </summary>
public class AssessmentEndpointsTests : IClassFixture<VokasiaApiFactory>
{
    private readonly VokasiaApiFactory _factory;
    public AssessmentEndpointsTests(VokasiaApiFactory factory) => _factory = factory;

    private async Task<HttpClient> AuthClientAsync(UserRole role, Guid? tenantId, string emailPrefix)
    {
        var user = await AuthTestHelpers.SeedUserAsync(_factory, emailPrefix, role, tenantId);
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var (accessToken, _) = await AuthTestHelpers.LoginAndExchangeAsync(client, user.Email!);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private sealed record Fixture(Guid TenantId, Guid PlacementId, Guid TeknisId, Guid SoftskillId, Guid KehadiranId, Guid MentorUserId);

    private async Task<Fixture> SeedFixtureAsync(Guid tenantId, Guid mentorUserId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();

        var teknis = new RubricAspect { Id = Guid.NewGuid(), Name = "Teknis", Kind = RubricAspectKind.Teknis, Weight = 40 };
        var softskill = new RubricAspect { Id = Guid.NewGuid(), Name = "Softskill", Kind = RubricAspectKind.Softskill, Weight = 40 };
        var kehadiran = new RubricAspect { Id = Guid.NewGuid(), Name = "Kehadiran", Kind = RubricAspectKind.Kehadiran, Weight = 20 };
        var rubric = new RubricTemplate { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Rubrik Uji", IsDefault = true, Aspects = [teknis, softskill, kehadiran] };
        foreach (var a in rubric.Aspects) a.RubricTemplateId = rubric.Id;

        var period = new Period { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Periode Uji", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 12, 31), ClassLevels = "XII", Status = PeriodStatus.Active };
        var company = new Company { Id = Guid.NewGuid(), Name = "PT Uji Nilai" };
        var student = new Student { Id = Guid.NewGuid(), TenantId = tenantId, FullName = "Siswa Uji Nilai", MajorId = Guid.NewGuid(), Classroom = "XII A" };
        var placement = new Placement { Id = Guid.NewGuid(), TenantId = tenantId, StudentId = student.Id, CompanyId = company.Id, PeriodId = period.Id, TeacherId = Guid.NewGuid(), MentorUserId = mentorUserId, Status = PlacementStatus.Active };

        db.RubricTemplates.Add(rubric);
        db.Periods.Add(period);
        db.Companies.Add(company);
        db.Students.Add(student);
        db.Placements.Add(placement);
        await db.SaveChangesAsync();

        return new Fixture(tenantId, placement.Id, teknis.Id, softskill.Id, kehadiran.Id, mentorUserId);
    }

    /// <summary>Placement KEDUA di period+rubric yg SAMA dgn `existing` (bukan rubric baru) - dipakai
    /// test batch-mode supaya `IsDefault` tenant tetap tunggal (2x SeedFixtureAsync pd tenant sama
    /// akan bikin 2 baris IsDefault=true, ResolveRubricAsync jadi ambigu - PROMPT-D-style ditemukan
    /// saat test batch gagal krn salah rubric ke-resolve).</summary>
    private async Task<Fixture> SeedSecondPlacementSameRubricAsync(Fixture existing, Guid mentorUserId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
        var periodId = await db.Placements.Where(p => p.Id == existing.PlacementId).Select(p => p.PeriodId).FirstAsync();
        var companyId = await db.Placements.Where(p => p.Id == existing.PlacementId).Select(p => p.CompanyId).FirstAsync();

        var student = new Student { Id = Guid.NewGuid(), TenantId = existing.TenantId, FullName = "Siswa Uji Nilai Kedua", MajorId = Guid.NewGuid(), Classroom = "XII B" };
        var placement = new Placement { Id = Guid.NewGuid(), TenantId = existing.TenantId, StudentId = student.Id, CompanyId = companyId, PeriodId = periodId, TeacherId = Guid.NewGuid(), MentorUserId = mentorUserId, Status = PlacementStatus.Active };
        db.Students.Add(student);
        db.Placements.Add(placement);
        await db.SaveChangesAsync();

        return existing with { PlacementId = placement.Id, MentorUserId = mentorUserId };
    }

    [Fact]
    public async Task SubmitMentorScores_ValidTeknisAndKehadiran_SetsMentorDoneTrue()
    {
        var tenantId = Guid.NewGuid();
        var mentor = await AuthTestHelpers.SeedUserAsync(_factory, "assess-mentor-ok", UserRole.IndustryMentor, null);
        var mentorClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var (token, _) = await AuthTestHelpers.LoginAndExchangeAsync(mentorClient, mentor.Email!);
        mentorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var fx = await SeedFixtureAsync(tenantId, mentor.Id);

        var resp = await mentorClient.PostAsJsonAsync($"/api/placements/{fx.PlacementId}/assessment/mentor-scores", new object[]
        {
            new { AspectId = fx.TeknisId, Value = 85m },
            new { AspectId = fx.KehadiranId, Value = 75m },
        });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("mentorDone").GetBoolean());
        Assert.False(body.GetProperty("teacherDone").GetBoolean());
    }

    [Fact]
    public async Task SubmitMentorScores_TriesSoftskillAspect_Returns400()
    {
        var tenantId = Guid.NewGuid();
        var mentor = await AuthTestHelpers.SeedUserAsync(_factory, "assess-mentor-wrongside", UserRole.IndustryMentor, null);
        var mentorClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var (token, _) = await AuthTestHelpers.LoginAndExchangeAsync(mentorClient, mentor.Email!);
        mentorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var fx = await SeedFixtureAsync(tenantId, mentor.Id);

        var resp = await mentorClient.PostAsJsonAsync($"/api/placements/{fx.PlacementId}/assessment/mentor-scores", new object[]
        {
            new { AspectId = fx.SoftskillId, Value = 90m },
        });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task SubmitMentorScores_NotOwnPlacement_Returns403()
    {
        var tenantId = Guid.NewGuid();
        var actualMentor = await AuthTestHelpers.SeedUserAsync(_factory, "assess-mentor-owner", UserRole.IndustryMentor, null);
        var intruder = await AuthTestHelpers.SeedUserAsync(_factory, "assess-mentor-intruder", UserRole.IndustryMentor, null);
        var intruderClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var (token, _) = await AuthTestHelpers.LoginAndExchangeAsync(intruderClient, intruder.Email!);
        intruderClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var fx = await SeedFixtureAsync(tenantId, actualMentor.Id);

        var resp = await intruderClient.PostAsJsonAsync($"/api/placements/{fx.PlacementId}/assessment/mentor-scores", new object[]
        {
            new { AspectId = fx.TeknisId, Value = 85m },
        });

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task SubmitTeacherScores_ValidSoftskill_SetsTeacherDoneTrue()
    {
        var tenantId = Guid.NewGuid();
        var mentor = await AuthTestHelpers.SeedUserAsync(_factory, "assess-teacher-mentorstub", UserRole.IndustryMentor, null);
        var teacherClient = await AuthClientAsync(UserRole.Teacher, tenantId, "assess-teacher-ok");
        var fx = await SeedFixtureAsync(tenantId, mentor.Id);

        var resp = await teacherClient.PostAsJsonAsync($"/api/placements/{fx.PlacementId}/assessment/teacher-scores", new object[]
        {
            new { AspectId = fx.SoftskillId, Value = 90m },
        });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("teacherDone").GetBoolean());
        Assert.False(body.GetProperty("mentorDone").GetBoolean());
    }

    [Fact]
    public async Task GetAssessment_BeforeAnyScoresSubmitted_ReturnsShellWithAspectsAndBothFalse()
    {
        var tenantId = Guid.NewGuid();
        var mentor = await AuthTestHelpers.SeedUserAsync(_factory, "assess-get-shell-mentor", UserRole.IndustryMentor, null);
        var adminClient = await AuthClientAsync(UserRole.TenantAdmin, tenantId, "assess-get-shell-admin");
        var fx = await SeedFixtureAsync(tenantId, mentor.Id);

        var resp = await adminClient.GetAsync($"/api/placements/{fx.PlacementId}/assessment");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, body.GetProperty("aspects").GetArrayLength());
        Assert.False(body.GetProperty("mentorDone").GetBoolean());
        Assert.False(body.GetProperty("teacherDone").GetBoolean());
        Assert.False(body.GetProperty("isFinal").GetBoolean());
    }

    [Fact]
    public async Task FinalizeAssessment_SinglePlacementAllScoresComplete_SetsFinalScoreAndLocks()
    {
        var tenantId = Guid.NewGuid();
        var mentor = await AuthTestHelpers.SeedUserAsync(_factory, "assess-finalize-ok-mentor", UserRole.IndustryMentor, null);
        var mentorClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var (mentorToken, _) = await AuthTestHelpers.LoginAndExchangeAsync(mentorClient, mentor.Email!);
        mentorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mentorToken);
        var teacherClient = await AuthClientAsync(UserRole.Teacher, tenantId, "assess-finalize-ok-teacher");
        var adminClient = await AuthClientAsync(UserRole.TenantAdmin, tenantId, "assess-finalize-ok-admin");

        var fx = await SeedFixtureAsync(tenantId, mentor.Id);

        await mentorClient.PostAsJsonAsync($"/api/placements/{fx.PlacementId}/assessment/mentor-scores", new object[]
        {
            new { AspectId = fx.TeknisId, Value = 85m },
            new { AspectId = fx.KehadiranId, Value = 75m },
        });
        await teacherClient.PostAsJsonAsync($"/api/placements/{fx.PlacementId}/assessment/teacher-scores", new object[]
        {
            new { AspectId = fx.SoftskillId, Value = 90m },
        });

        // Kasus manual sama persis AssessmentScoringTests #2: (85*40+90*40+75*20)/100 = 85.00.
        var finalizeResp = await adminClient.PostAsJsonAsync($"/api/periods/{(await GetPeriodIdAsync(fx.PlacementId))}/assessments/finalize", new { PeriodId = await GetPeriodIdAsync(fx.PlacementId), PlacementId = fx.PlacementId });

        Assert.Equal(HttpStatusCode.OK, finalizeResp.StatusCode);
        var finalizeBody = await finalizeResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Single(finalizeBody.GetProperty("finalized").EnumerateArray());
        Assert.Equal(0, finalizeBody.GetProperty("incomplete").GetArrayLength());

        var getResp = await adminClient.GetAsync($"/api/placements/{fx.PlacementId}/assessment");
        var getBody = await getResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(getBody.GetProperty("isFinal").GetBoolean());
        Assert.Equal(85.00m, getBody.GetProperty("finalScore").GetDecimal());

        // AC: revisi skor SETELAH final -> 409.
        var reviseResp = await mentorClient.PostAsJsonAsync($"/api/placements/{fx.PlacementId}/assessment/mentor-scores", new object[]
        {
            new { AspectId = fx.TeknisId, Value = 50m },
        });
        Assert.Equal(HttpStatusCode.Conflict, reviseResp.StatusCode);

        // Idempoten: finalize KEDUA kalinya utk placement yg sama tak masuk finalized/incomplete
        // lagi (sudah final sebelumnya, dilewati diam-diam) - PROMPT D membuktikan cek ini nyata
        // (lihat commit message: `if (true) continue` sengaja dipasang sesi ini -> collection
        // kosong -> Assert.Single gagal -> dikembalikan -> hijau lagi).
        var periodId = await GetPeriodIdAsync(fx.PlacementId);
        var secondFinalizeResp = await adminClient.PostAsJsonAsync($"/api/periods/{periodId}/assessments/finalize", new { PeriodId = periodId, PlacementId = fx.PlacementId });
        Assert.Equal(HttpStatusCode.OK, secondFinalizeResp.StatusCode);
        var secondBody = await secondFinalizeResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, secondBody.GetProperty("finalized").GetArrayLength());
        Assert.Equal(0, secondBody.GetProperty("incomplete").GetArrayLength());
    }

    [Fact]
    public async Task FinalizeAssessment_SinglePlacementIncomplete_Returns422WithMissingAspectNames()
    {
        var tenantId = Guid.NewGuid();
        var mentor = await AuthTestHelpers.SeedUserAsync(_factory, "assess-finalize-incomplete-mentor", UserRole.IndustryMentor, null);
        var mentorClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var (mentorToken, _) = await AuthTestHelpers.LoginAndExchangeAsync(mentorClient, mentor.Email!);
        mentorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mentorToken);
        var adminClient = await AuthClientAsync(UserRole.TenantAdmin, tenantId, "assess-finalize-incomplete-admin");

        var fx = await SeedFixtureAsync(tenantId, mentor.Id);

        // Hanya mentor isi - guru belum sama sekali.
        await mentorClient.PostAsJsonAsync($"/api/placements/{fx.PlacementId}/assessment/mentor-scores", new object[]
        {
            new { AspectId = fx.TeknisId, Value = 85m },
            new { AspectId = fx.KehadiranId, Value = 75m },
        });

        var periodId = await GetPeriodIdAsync(fx.PlacementId);
        var resp = await adminClient.PostAsJsonAsync($"/api/periods/{periodId}/assessments/finalize", new { PeriodId = periodId, PlacementId = fx.PlacementId });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var incomplete = body.GetProperty("incomplete");
        Assert.Equal(1, incomplete.GetArrayLength());
        Assert.Contains("Softskill", incomplete[0].GetProperty("missingAspectNames")[0].GetString());
    }

    [Fact]
    public async Task FinalizeAssessment_BatchPeriodMode_FinalizesCompleteAndReportsIncompleteSeparately()
    {
        var tenantId = Guid.NewGuid();
        var mentorA = await AuthTestHelpers.SeedUserAsync(_factory, "assess-batch-mentorA", UserRole.IndustryMentor, null);
        var mentorB = await AuthTestHelpers.SeedUserAsync(_factory, "assess-batch-mentorB", UserRole.IndustryMentor, null);
        var teacherClient = await AuthClientAsync(UserRole.Teacher, tenantId, "assess-batch-teacher");
        var adminClient = await AuthClientAsync(UserRole.TenantAdmin, tenantId, "assess-batch-admin");

        var fxA = await SeedFixtureAsync(tenantId, mentorA.Id); // akan lengkap.
        var fxB = await SeedSecondPlacementSameRubricAsync(fxA, mentorB.Id); // sengaja TIDAK lengkap.

        var mentorAClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var (tokenA, _) = await AuthTestHelpers.LoginAndExchangeAsync(mentorAClient, mentorA.Email!);
        mentorAClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);

        await mentorAClient.PostAsJsonAsync($"/api/placements/{fxA.PlacementId}/assessment/mentor-scores", new object[]
        {
            new { AspectId = fxA.TeknisId, Value = 85m },
            new { AspectId = fxA.KehadiranId, Value = 75m },
        });
        await teacherClient.PostAsJsonAsync($"/api/placements/{fxA.PlacementId}/assessment/teacher-scores", new object[]
        {
            new { AspectId = fxA.SoftskillId, Value = 90m },
        });
        // fxB: TIDAK ada skor sama sekali diisi.

        var periodId = await GetPeriodIdAsync(fxA.PlacementId);
        var resp = await adminClient.PostAsJsonAsync($"/api/periods/{periodId}/assessments/finalize", new { PeriodId = periodId, PlacementId = (Guid?)null });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode); // mode batch: 200 walau ada yg incomplete (best-effort).
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetProperty("finalized").GetArrayLength());
        Assert.Equal(fxA.PlacementId, body.GetProperty("finalized")[0].GetGuid());
        Assert.Equal(1, body.GetProperty("incomplete").GetArrayLength());
        Assert.Equal(fxB.PlacementId, body.GetProperty("incomplete")[0].GetProperty("placementId").GetGuid());
    }

    private async Task<Guid> GetPeriodIdAsync(Guid placementId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
        return await db.Placements.Where(p => p.Id == placementId).Select(p => p.PeriodId).FirstAsync();
    }

    // ---------- ListMentorAssessmentPlacements (VOK-H5-E2, gap ditambal — lihat DECISIONS.md D34) ----------

    private async Task<(Guid PlacementId, Guid MentorUserId)> SeedMentorPlacementAsync(PeriodStatus periodStatus, Guid mentorUserId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
        var tenantId = Guid.NewGuid();
        var mentorId = mentorUserId;
        var period = new Period { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Periode Antrean Nilai", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 12, 31), ClassLevels = "XII", Status = periodStatus };
        var company = new Company { Id = Guid.NewGuid(), Name = "PT Antrean Nilai" };
        var student = new Student { Id = Guid.NewGuid(), TenantId = tenantId, FullName = "Siswa Antrean Nilai", MajorId = Guid.NewGuid(), Classroom = "XII A" };
        var placement = new Placement { Id = Guid.NewGuid(), TenantId = tenantId, StudentId = student.Id, CompanyId = company.Id, PeriodId = period.Id, TeacherId = Guid.NewGuid(), MentorUserId = mentorId, Status = PlacementStatus.Active };

        db.Periods.Add(period);
        db.Companies.Add(company);
        db.Students.Add(student);
        db.Placements.Add(placement);
        await db.SaveChangesAsync();
        return (placement.Id, mentorId);
    }

    private async Task<HttpClient> MentorClientAsync(AppUser mentor)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var (token, _) = await AuthTestHelpers.LoginAndExchangeAsync(client, mentor.Email!);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task ListMentorAssessmentPlacements_PeriodInAssessmentPhase_ReturnsPlacement()
    {
        var mentor = await AuthTestHelpers.SeedUserAsync(_factory, "queue-mentor-ok", UserRole.IndustryMentor, null);
        var (placementId, _) = await SeedMentorPlacementAsync(PeriodStatus.Assessment, mentor.Id);
        var client = await MentorClientAsync(mentor);

        var resp = await client.GetAsync("/api/mentors/assessment-queue");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetArrayLength());
        Assert.Equal(placementId, body[0].GetProperty("placementId").GetGuid());
        Assert.Equal("Siswa Antrean Nilai", body[0].GetProperty("studentName").GetString());
    }

    [Fact]
    public async Task ListMentorAssessmentPlacements_PeriodNotAssessmentPhase_ExcludesIt()
    {
        var mentor = await AuthTestHelpers.SeedUserAsync(_factory, "queue-mentor-notyet", UserRole.IndustryMentor, null);
        await SeedMentorPlacementAsync(PeriodStatus.Active, mentor.Id);
        var client = await MentorClientAsync(mentor);

        var resp = await client.GetAsync("/api/mentors/assessment-queue");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, body.GetArrayLength());
    }

    [Fact]
    public async Task ListMentorAssessmentPlacements_OtherMentorsPlacement_NotIncluded()
    {
        var otherMentor = await AuthTestHelpers.SeedUserAsync(_factory, "queue-mentor-other", UserRole.IndustryMentor, null);
        await SeedMentorPlacementAsync(PeriodStatus.Assessment, otherMentor.Id); // milik mentor LAIN.
        var myMentor = await AuthTestHelpers.SeedUserAsync(_factory, "queue-mentor-scoped", UserRole.IndustryMentor, null);
        var client = await MentorClientAsync(myMentor);

        var resp = await client.GetAsync("/api/mentors/assessment-queue");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, body.GetArrayLength());
    }

    [Fact]
    public async Task ListMentorAssessmentPlacements_TeacherRole_Forbidden()
    {
        var client = await AuthClientAsync(UserRole.Teacher, Guid.NewGuid(), "queue-teacher-forbidden");

        var resp = await client.GetAsync("/api/mentors/assessment-queue");

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }
}
