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

/// <summary>
/// VOK-H5-E1 §2 — CreateRubricTemplate/UpdateRubric (TenantAdmin) + GetRubric (TenantMember).
/// Lihat doc-comment RubricEndpoints ttg [CAKUPAN] GetRubric(periodId) = rubric default TENANT
/// (bukan spesifik periode - skema H1-E1 tak punya kolom itu).
/// </summary>
public class RubricEndpointsTests : IClassFixture<VokasiaApiFactory>
{
    private readonly VokasiaApiFactory _factory;
    public RubricEndpointsTests(VokasiaApiFactory factory) => _factory = factory;

    private async Task<HttpClient> AuthenticatedAdminClientAsync(Guid tenantId, string emailPrefix = "rubric-admin")
    {
        var user = await AuthTestHelpers.SeedUserAsync(_factory, emailPrefix, UserRole.TenantAdmin, tenantId);
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var (accessToken, _) = await AuthTestHelpers.LoginAndExchangeAsync(client, user.Email!);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    // Kind dikirim sbg INT (System.Text.Json default, TIDAK ada JsonStringEnumConverter terdaftar
    // di Program.cs - dikonfirmasi PROMPT-D-style debugging sesi ini: kirim "Teknis" sbg string
    // -> 500 JsonException "could not be converted to RubricAspectKind". Konvensi SAMA persis dgn
    // enum lain di codebase - lihat JournalStudentEndpointsTests yg assert body enum via GetInt32()).
    private const int Teknis = (int)RubricAspectKind.Teknis;
    private const int Softskill = (int)RubricAspectKind.Softskill;
    private const int Kehadiran = (int)RubricAspectKind.Kehadiran;

    private static object ValidCreateRequest(string name = "Rubrik Kurikulum Merdeka") => new
    {
        Name = name,
        Aspects = new object[]
        {
            new { Name = "Teknis", Kind = Teknis, Weight = 40 },
            new { Name = "Softskill", Kind = Softskill, Weight = 40 },
            new { Name = "Kehadiran", Kind = Kehadiran, Weight = 20 },
        },
    };

    [Fact]
    public async Task CreateRubricTemplate_ValidWeights_PersistsAsDefaultForFirstTenantRubric()
    {
        var tenantId = Guid.NewGuid();
        var client = await AuthenticatedAdminClientAsync(tenantId);

        var resp = await client.PostAsJsonAsync("/api/rubrics", ValidCreateRequest());

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("isDefault").GetBoolean());
        Assert.Equal(3, body.GetProperty("aspects").GetArrayLength());
    }

    [Fact]
    public async Task CreateRubricTemplate_WeightsDoNotSumTo100_Returns422()
    {
        var tenantId = Guid.NewGuid();
        var client = await AuthenticatedAdminClientAsync(tenantId, "rubric-badweight");

        var resp = await client.PostAsJsonAsync("/api/rubrics", new
        {
            Name = "Rubrik Salah",
            Aspects = new object[]
            {
                new { Name = "Teknis", Kind = Teknis, Weight = 40 },
                new { Name = "Softskill", Kind = Softskill, Weight = 40 },
                // total cuma 80, bukan 100.
            },
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resp.StatusCode);
    }

    [Fact]
    public async Task CreateRubricTemplate_SecondRubricForSameTenant_IsNotDefault()
    {
        var tenantId = Guid.NewGuid();
        var client = await AuthenticatedAdminClientAsync(tenantId, "rubric-second");

        var first = await client.PostAsJsonAsync("/api/rubrics", ValidCreateRequest("Rubrik Pertama"));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/rubrics", ValidCreateRequest("Rubrik Kedua"));
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var body = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("isDefault").GetBoolean());
    }

    [Fact]
    public async Task UpdateRubric_NotUsedByFinalAssessment_ReplacesAspects()
    {
        var tenantId = Guid.NewGuid();
        var client = await AuthenticatedAdminClientAsync(tenantId, "rubric-update-ok");

        var created = await client.PostAsJsonAsync("/api/rubrics", ValidCreateRequest());
        var createdBody = await created.Content.ReadFromJsonAsync<JsonElement>();
        var rubricId = createdBody.GetProperty("id").GetGuid();

        var resp = await client.PutAsJsonAsync($"/api/rubrics/{rubricId}", new
        {
            Name = "Rubrik Direvisi",
            Aspects = new object[] { new { Name = "Teknis Saja", Kind = Teknis, Weight = 100 } },
        });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Rubrik Direvisi", body.GetProperty("name").GetString());
        Assert.Equal(1, body.GetProperty("aspects").GetArrayLength());
    }

    [Fact]
    public async Task UpdateRubric_UsedByFinalAssessment_Returns409()
    {
        var tenantId = Guid.NewGuid();
        var client = await AuthenticatedAdminClientAsync(tenantId, "rubric-update-locked");

        var created = await client.PostAsJsonAsync("/api/rubrics", ValidCreateRequest());
        var createdBody = await created.Content.ReadFromJsonAsync<JsonElement>();
        var rubricId = createdBody.GetProperty("id").GetGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            db.Assessments.Add(new Vokasia.Domain.Entities.Assessment
            {
                Id = Guid.NewGuid(), TenantId = tenantId, PlacementId = Guid.NewGuid(),
                RubricTemplateId = rubricId, IsFinal = true, FinalScore = 88.5m, FinalizedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var resp = await client.PutAsJsonAsync($"/api/rubrics/{rubricId}", new
        {
            Name = "Coba Ubah Setelah Final",
            Aspects = new object[] { new { Name = "Teknis Saja", Kind = Teknis, Weight = 100 } },
        });

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task GetRubric_PeriodExists_ReturnsTenantDefaultRubric()
    {
        var tenantId = Guid.NewGuid();
        var client = await AuthenticatedAdminClientAsync(tenantId, "rubric-get-ok");

        await client.PostAsJsonAsync("/api/rubrics", ValidCreateRequest());

        Guid periodId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            var period = new Period { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Periode Rubrik", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 12, 31), ClassLevels = "XII", Status = PeriodStatus.Active };
            periodId = period.Id;
            db.Periods.Add(period);
            await db.SaveChangesAsync();
        }

        var resp = await client.GetAsync($"/api/periods/{periodId}/rubric");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("isDefault").GetBoolean());
    }

    [Fact]
    public async Task GetRubric_PeriodNotFound_Returns404()
    {
        var tenantId = Guid.NewGuid();
        var client = await AuthenticatedAdminClientAsync(tenantId, "rubric-get-404period");

        var resp = await client.GetAsync($"/api/periods/{Guid.NewGuid()}/rubric");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task GetRubric_NoDefaultRubricYetForTenant_Returns404()
    {
        var tenantId = Guid.NewGuid();
        var client = await AuthenticatedAdminClientAsync(tenantId, "rubric-get-nodefault");

        Guid periodId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            var period = new Period { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Periode Tanpa Rubrik", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 12, 31), ClassLevels = "XII", Status = PeriodStatus.Active };
            periodId = period.Id;
            db.Periods.Add(period);
            await db.SaveChangesAsync();
        }

        var resp = await client.GetAsync($"/api/periods/{periodId}/rubric");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
