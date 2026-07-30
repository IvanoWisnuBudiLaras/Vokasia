using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vokasia.Api.Auth;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Tests.Integration;

/// <summary>
/// VOK-H5-E3 §1 MagicLinkLifecycleTests — invite (guru) -&gt; exchange (/connect/token grant kustom
/// magic-link) -&gt; sesi mentor SCOPED ke placement yang diundang; token dipakai ulang / kedaluwarsa
/// -&gt; ditolak (400 invalid_grant). FR-AUTH-03. Postgres Testcontainers sungguhan - MentorInvite
/// row + TokenHash lookup adalah query SQL nyata, bukan Dictionary in-memory.
/// </summary>
[Collection("IntegrationTests")]
public class MagicLinkLifecycleTests
{
    private readonly VokasiaIntegrationFactory _factory;
    public MagicLinkLifecycleTests(VokasiaIntegrationFactory factory) => _factory = factory;

    /// <summary>Rubrik default tenant - GetAssessment 404 kalau tenant belum punya rubrik IsDefault
    /// sama sekali (di luar isu scope mentor) - dipanggil SEKALI per tenant (bukan per placement,
    /// 2x akan bikin 2 baris IsDefault=true, ResolveRubricAsync jadi ambigu - gotcha yg sama persis
    /// dicatat AssessmentEndpointsTests.SeedSecondPlacementSameRubricAsync).</summary>
    private async Task SeedDefaultRubricAsync(Guid tenantId)
    {
        using var scope = _factory.CreateDbScope();
        var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
        var teknis = new RubricAspect { Id = Guid.NewGuid(), Name = "Teknis", Kind = RubricAspectKind.Teknis, Weight = 100 };
        var rubric = new RubricTemplate { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Rubrik Magic Link", IsDefault = true, Aspects = [teknis] };
        teknis.RubricTemplateId = rubric.Id;
        db.RubricTemplates.Add(rubric);
        await db.SaveChangesAsync();
    }

    private async Task<Guid> SeedPlacementWithMentorEmailAsync(Guid tenantId, string mentorEmail)
    {
        using var scope = _factory.CreateDbScope();
        var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();

        var period = new Period { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Periode Magic Link", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 12, 31), ClassLevels = "XII", Status = PeriodStatus.Active };
        var company = new Company { Id = Guid.NewGuid(), Name = "PT Magic Link" };
        var student = new Student { Id = Guid.NewGuid(), TenantId = tenantId, FullName = "Siswa Magic Link", MajorId = Guid.NewGuid(), Classroom = "XII A" };
        var placement = new Placement
        {
            Id = Guid.NewGuid(), TenantId = tenantId, StudentId = student.Id, CompanyId = company.Id,
            PeriodId = period.Id, TeacherId = Guid.NewGuid(), Status = PlacementStatus.Active, MentorEmail = mentorEmail,
        };

        db.Periods.Add(period);
        db.Companies.Add(company);
        db.Students.Add(student);
        db.Placements.Add(placement);
        await db.SaveChangesAsync();
        return placement.Id;
    }

    private static string ExtractTokenFromMagicLinkUrl(string url) =>
        new Uri(url).Query.TrimStart('?').Split('&').Select(p => p.Split('=')).First(kv => kv[0] == "token")[1];

    private async Task<HttpResponseMessage> ExchangeAsync(HttpClient client, string rawToken)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = OpenIddictSetup.MagicLinkGrantType,
            ["token"] = rawToken,
            ["client_id"] = OpenIddictSetup.BffClientId,
            ["client_secret"] = Vokasia.Tests.Auth.AuthTestHelpers.ClientSecret,
        };
        return await client.PostAsync("/connect/token", new FormUrlEncodedContent(form));
    }

    [Fact]
    public async Task Invite_Exchange_IssuesSessionScopedToInvitedPlacement()
    {
        var tenant = await _factory.SeedTenantAsync();
        await SeedDefaultRubricAsync(tenant.Id);
        var placementId = await SeedPlacementWithMentorEmailAsync(tenant.Id, $"mentor-{Guid.NewGuid():N}@vokasia.test");
        var (_, teacherClient) = await _factory.LoginAsAsync(UserRole.Teacher, tenant.Id, "magiclink-teacher-invite");

        var createResp = await teacherClient.PostAsJsonAsync("/api/mentor-invites", new { PlacementId = placementId, MentorName = "Mentor Uji" });
        createResp.EnsureSuccessStatusCode();
        var inviteBody = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var magicLinkUrl = inviteBody.GetProperty("magicLinkUrl").GetString()!;
        var rawToken = ExtractTokenFromMagicLinkUrl(magicLinkUrl);

        var anonClient = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var exchangeResp = await ExchangeAsync(anonClient, rawToken);
        Assert.Equal(HttpStatusCode.OK, exchangeResp.StatusCode);

        var tokenJson = await exchangeResp.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = tokenJson.GetProperty("access_token").GetString()!;

        // Sesi mentor SCOPED placement: bisa lihat assessment placement yang diundang, TIDAK bisa lihat placement lain (MentorOwnPlacement).
        var mentorReq = new HttpRequestMessage(HttpMethod.Get, $"/api/placements/{placementId}/assessment");
        mentorReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        var ownResp = await anonClient.SendAsync(mentorReq);
        Assert.Equal(HttpStatusCode.OK, ownResp.StatusCode);

        var otherPlacementId = await SeedPlacementWithMentorEmailAsync(tenant.Id, $"other-{Guid.NewGuid():N}@vokasia.test");
        var otherReq = new HttpRequestMessage(HttpMethod.Get, $"/api/placements/{otherPlacementId}/assessment");
        otherReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        var otherResp = await anonClient.SendAsync(otherReq);
        Assert.Equal(HttpStatusCode.Forbidden, otherResp.StatusCode);
    }

    [Fact]
    public async Task Exchange_TokenReused_Rejected()
    {
        var tenant = await _factory.SeedTenantAsync();
        var placementId = await SeedPlacementWithMentorEmailAsync(tenant.Id, $"mentor-{Guid.NewGuid():N}@vokasia.test");
        var (_, teacherClient) = await _factory.LoginAsAsync(UserRole.Teacher, tenant.Id, "magiclink-teacher-reuse");

        var createResp = await teacherClient.PostAsJsonAsync("/api/mentor-invites", new { PlacementId = placementId, MentorName = "Mentor Reuse" });
        createResp.EnsureSuccessStatusCode();
        var inviteBody = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var rawToken = ExtractTokenFromMagicLinkUrl(inviteBody.GetProperty("magicLinkUrl").GetString()!);

        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var firstResp = await ExchangeAsync(client, rawToken);
        Assert.Equal(HttpStatusCode.OK, firstResp.StatusCode);

        var secondResp = await ExchangeAsync(client, rawToken);
        Assert.Equal(HttpStatusCode.BadRequest, secondResp.StatusCode);
        var errorBody = await secondResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_grant", errorBody.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Exchange_ExpiredToken_Rejected()
    {
        var tenant = await _factory.SeedTenantAsync();
        var placementId = await SeedPlacementWithMentorEmailAsync(tenant.Id, $"mentor-{Guid.NewGuid():N}@vokasia.test");
        var (_, teacherClient) = await _factory.LoginAsAsync(UserRole.Teacher, tenant.Id, "magiclink-teacher-expired");

        var createResp = await teacherClient.PostAsJsonAsync("/api/mentor-invites", new { PlacementId = placementId, MentorName = "Mentor Expired" });
        createResp.EnsureSuccessStatusCode();
        var inviteBody = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var inviteId = inviteBody.GetProperty("id").GetGuid();
        var rawToken = ExtractTokenFromMagicLinkUrl(inviteBody.GetProperty("magicLinkUrl").GetString()!);

        // TTL 72 jam (MagicLinkService.Ttl) tidak bisa ditunggu sungguhan di test cepat - mundurkan
        // ExpiresAt langsung di DB (baris sudah ada dari CreateInvite di atas, hanya kolom TTL yang
        // dimanipulasi, TokenHash/logika exchange TETAP kode produksi asli yang diuji apa adanya).
        using (var scope = _factory.CreateDbScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            var invite = await db.MentorInvites.FirstAsync(i => i.Id == inviteId);
            invite.ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1);
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var resp = await ExchangeAsync(client, rawToken);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var errorBody = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_grant", errorBody.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Exchange_UnknownToken_Rejected()
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var resp = await ExchangeAsync(client, "token-yang-tidak-pernah-ada-sama-sekali");
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
