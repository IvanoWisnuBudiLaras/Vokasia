using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using Vokasia.Api.Auth;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Tests.Auth;

namespace Vokasia.Tests.Security;

/// <summary>
/// AC VOK-H2-E3 §4 RbacPolicyTests ("sampel matrix per role, mis. Teacher approve jurnal -> 403")
/// — slice yang sempat ditunda, ditulis menyusul. Endpoint ApproveJournal belum ada (H3-E1), jadi
/// sampel matrix dipakai dari endpoint RIL yang SUDAH ada H2-E1 (Periods, SchoolUsers) — cukup
/// utk membuktikan 3 policy berbeda (DeptHeadPlus/TenantAdminOnly/TenantMember) benar-benar
/// membedakan role, lewat Bearer token JWT SUNGGUHAN (AuthTestHelpers, dance code+PKCE penuh —
/// bukan cuma baca konfigurasi RbacPolicies.cs). PlacementScopeHandler (MentorOwnPlacement) diuji
/// terpisah sbg unit test langsung di bawah — belum ada endpoint HTTP nyata yang memakainya
/// (dipakai mulai ApproveJournal H3, per komentar RbacPolicies.cs sendiri), jadi tak bisa diuji
/// lewat HTTP tanpa endpoint fiktif; unit test langsung thd handler TETAP bukti nyata (bukan
/// asumsi) logikanya benar begitu endpoint H3 memakainya.
/// </summary>
public class RbacPolicyTests : IClassFixture<VokasiaApiFactory>
{
    private readonly VokasiaApiFactory _factory;
    public RbacPolicyTests(VokasiaApiFactory factory) => _factory = factory;

    private async Task<HttpClient> AuthenticatedClientAsync(UserRole role, Guid? tenantId)
    {
        var user = await AuthTestHelpers.SeedUserAsync(_factory, role.ToString().ToLowerInvariant(), role, tenantId);
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var (accessToken, _) = await AuthTestHelpers.LoginAndExchangeAsync(client, user.Email!);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private static StringContent JsonBody(object payload) =>
        new(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

    // --- RbacPolicies.DeptHeadPlus (TenantAdmin, DeptHead) — POST /api/periods ---

    [Fact]
    public async Task Teacher_CreatePeriod_IsForbidden()
    {
        // DeptHeadPlus SENGAJA tak termasuk Teacher (beda dari TeacherPlus) — Teacher boleh baca
        // (TenantMember), tapi tak boleh buat periode.
        var client = await AuthenticatedClientAsync(UserRole.Teacher, Guid.NewGuid());
        var body = JsonBody(new { Name = "Periode Uji", StartDate = "2026-01-01", EndDate = "2026-06-30", ClassLevels = new[] { "XII" }, Holidays = (object?)null });

        var resp = await client.PostAsync("/api/periods", body);

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Student_CreatePeriod_IsForbidden()
    {
        var client = await AuthenticatedClientAsync(UserRole.Student, Guid.NewGuid());
        var body = JsonBody(new { Name = "Periode Uji", StartDate = "2026-01-01", EndDate = "2026-06-30", ClassLevels = new[] { "XII" }, Holidays = (object?)null });

        var resp = await client.PostAsync("/api/periods", body);

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task DeptHead_CreatePeriod_IsNotForbidden()
    {
        var client = await AuthenticatedClientAsync(UserRole.DeptHead, Guid.NewGuid());
        var body = JsonBody(new { Name = "Periode Uji", StartDate = "2026-01-01", EndDate = "2026-06-30", ClassLevels = new[] { "XII" }, Holidays = (object?)null });

        var resp = await client.PostAsync("/api/periods", body);

        // Fokus test ini GERBANG RBAC, bukan hasil bisnis penuh — assert bukan-403 (gerbang lolos),
        // bukan status sukses spesifik (yang bisa dipengaruhi hal lain di luar cakupan RBAC).
        Assert.NotEqual(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task DeptHeadWithoutTenantClaim_UpdatePeriod_IsForbidden()
    {
        var client = await AuthenticatedClientAsync(UserRole.DeptHead, null);
        var body = JsonBody(new
        {
            Name = "Periode Tanpa Tenant",
            StartDate = "2026-01-01",
            EndDate = "2026-06-30",
            ClassLevels = new[] { "XII" },
            Holidays = (object?)null,
        });

        var resp = await client.PutAsync($"/api/periods/{Guid.NewGuid()}", body);

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // --- RbacPolicies.TenantAdminOnly — POST /api/school-users (LEBIH KETAT dari DeptHeadPlus) ---

    [Fact]
    public async Task DeptHead_InviteSchoolUser_IsForbidden()
    {
        // Kasus matrix penting: DeptHead LOLOS DeptHeadPlus (CreatePeriod) tapi TIDAK lolos
        // TenantAdminOnly (InviteSchoolUser) — membuktikan 2 policy ini benar2 beda ketat, bukan alias.
        var client = await AuthenticatedClientAsync(UserRole.DeptHead, Guid.NewGuid());
        var body = JsonBody(new { Email = $"undangan-{Guid.NewGuid():N}@vokasia.test", FullName = "Calon Guru", Role = nameof(UserRole.Teacher) });

        var resp = await client.PostAsync("/api/school-users", body);

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task TenantAdmin_InviteSchoolUser_IsNotForbidden()
    {
        var client = await AuthenticatedClientAsync(UserRole.TenantAdmin, Guid.NewGuid());
        var body = JsonBody(new { Email = $"undangan-{Guid.NewGuid():N}@vokasia.test", FullName = "Calon Guru", Role = nameof(UserRole.Teacher) });

        var resp = await client.PostAsync("/api/school-users", body);

        Assert.NotEqual(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task TenantAdminWithoutTenantClaim_UpdateRubric_IsForbidden()
    {
        var client = await AuthenticatedClientAsync(UserRole.TenantAdmin, null);
        var body = JsonBody(new
        {
            Name = "Rubrik Tanpa Tenant",
            Aspects = Array.Empty<object>(),
        });

        var resp = await client.PutAsync($"/api/rubrics/{Guid.NewGuid()}", body);

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // --- RbacPolicies.TenantMember (siapa saja berklaim tenant_id) — GET /api/periods ---

    [Fact]
    public async Task Teacher_ListPeriods_IsNotForbidden()
    {
        var client = await AuthenticatedClientAsync(UserRole.Teacher, Guid.NewGuid());

        var resp = await client.GetAsync("/api/periods");

        Assert.NotEqual(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task TeacherWithoutTenantClaim_ListCompetencies_IsForbidden()
    {
        var client = await AuthenticatedClientAsync(UserRole.Teacher, null);

        var resp = await client.GetAsync("/api/competencies");

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task StudentWithoutTenantClaim_GetTodayJournal_IsForbidden()
    {
        var client = await AuthenticatedClientAsync(UserRole.Student, null);

        var resp = await client.GetAsync("/api/journals/today");

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Theory]
    [InlineData(nameof(UserRole.TenantAdmin), RbacPolicies.TenantAdminOnly)]
    [InlineData(nameof(UserRole.DeptHead), RbacPolicies.DeptHeadPlus)]
    [InlineData(nameof(UserRole.Teacher), RbacPolicies.TeacherPlus)]
    [InlineData(nameof(UserRole.Student), RbacPolicies.StudentSelf)]
    [InlineData(nameof(UserRole.Teacher), RbacPolicies.TenantMember)]
    public async Task TenantPolicies_MalformedTenantClaim_IsForbidden(
        string role,
        string policy)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddVokasiaRbacPolicies();
        await using var provider = services.BuildServiceProvider();
        var authorization = provider.GetRequiredService<IAuthorizationService>();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("role", role),
            new Claim("tenant_id", "not-a-guid"),
        ], "test"));

        var result = await authorization.AuthorizeAsync(principal, null, policy);

        Assert.False(result.Succeeded);
    }

    [Theory]
    [InlineData(nameof(UserRole.TenantAdmin), RbacPolicies.TenantAdminOnly)]
    [InlineData(nameof(UserRole.DeptHead), RbacPolicies.DeptHeadPlus)]
    [InlineData(nameof(UserRole.Teacher), RbacPolicies.TeacherPlus)]
    [InlineData(nameof(UserRole.Student), RbacPolicies.StudentSelf)]
    [InlineData(nameof(UserRole.Teacher), RbacPolicies.TenantMember)]
    public async Task TenantPolicies_MissingTenantClaim_IsForbidden(
        string role,
        string policy)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddVokasiaRbacPolicies();
        await using var provider = services.BuildServiceProvider();
        var authorization = provider.GetRequiredService<IAuthorizationService>();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("role", role),
        ], "test"));

        var result = await authorization.AuthorizeAsync(principal, null, policy);

        Assert.False(result.Succeeded);
    }

    // --- PlacementScopeHandler (MentorOwnPlacement) — unit test langsung, belum ada endpoint HTTP nyata ---

    [Fact]
    public async Task PlacementScopeHandler_MentorOwnsPlacement_Succeeds()
    {
        var mentorId = Guid.NewGuid();
        var placement = new Placement
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            StudentId = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            PeriodId = Guid.NewGuid(),
            TeacherId = Guid.NewGuid(),
            MentorUserId = mentorId,
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(OpenIddictConstants.Claims.Subject, mentorId.ToString()),
        }));
        var requirement = new PlacementScopeRequirement();
        var context = new AuthorizationHandlerContext(new[] { requirement }, principal, placement);

        await new PlacementScopeHandler().HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task PlacementScopeHandler_MentorDoesNotOwnPlacement_Fails()
    {
        var placement = new Placement
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            StudentId = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            PeriodId = Guid.NewGuid(),
            TeacherId = Guid.NewGuid(),
            MentorUserId = Guid.NewGuid(), // BEDA dari sub di bawah — mentor lain, bukan pemilik placement ini.
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(OpenIddictConstants.Claims.Subject, Guid.NewGuid().ToString()),
        }));
        var requirement = new PlacementScopeRequirement();
        var context = new AuthorizationHandlerContext(new[] { requirement }, principal, placement);

        await new PlacementScopeHandler().HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }
}
