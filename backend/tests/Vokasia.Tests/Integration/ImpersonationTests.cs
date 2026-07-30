using System.Net;
using System.Net.Http.Headers;
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
/// VOK-H6-E3 §1 ImpersonationTests — StartImpersonation (grant kustom OpenIddict) menukar identitas
/// PENUH: token baru ber-role/tenant_id milik
/// TARGET, RBAC endpoint SaOnly/TenantAdminOnly menegakkan identitas BARU itu apa adanya. AC ticket
/// literal: "audit log mencatat actor=SA, as=user" — diuji lewat VokasiaDbContext.SaveChangesAsync
/// (satu titik koreksi, lihat doc-comment di sana), BUKAN dgn menyuntik actingAsId ke tiap endpoint.
/// </summary>
[Collection("IntegrationTests")]
public class ImpersonationTests
{
    private readonly VokasiaIntegrationFactory _factory;
    public ImpersonationTests(VokasiaIntegrationFactory factory) => _factory = factory;

    private async Task<HttpResponseMessage> StartImpersonationAsync(HttpClient callerClient, Guid targetUserId)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = OpenIddictSetup.ImpersonationGrantType,
            ["target_user_id"] = targetUserId.ToString(),
            ["client_id"] = OpenIddictSetup.BffClientId,
            ["client_secret"] = Vokasia.Tests.Auth.AuthTestHelpers.ClientSecret,
        };
        return await callerClient.PostAsync("/connect/token", new FormUrlEncodedContent(form));
    }

    private HttpClient AnonClientWithBearer(string accessToken)
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    [Fact]
    public async Task StartImpersonation_AsSuperAdmin_IssuesTokenWithFullTargetIdentity()
    {
        var tenant = await _factory.SeedTenantAsync();
        var (saUser, saClient) = await _factory.LoginAsAsync(UserRole.SuperAdmin, null, "impersonate-sa");
        var target = await Vokasia.Tests.Auth.AuthTestHelpers.SeedUserAsync(_factory, "impersonate-target-admin", UserRole.TenantAdmin, tenant.Id);

        var startResp = await StartImpersonationAsync(saClient, target.Id);
        Assert.Equal(HttpStatusCode.OK, startResp.StatusCode);
        var tokenJson = await startResp.Content.ReadFromJsonAsync<JsonElement>();
        var impersonationToken = tokenJson.GetProperty("access_token").GetString()!;

        var asTargetClient = AnonClientWithBearer(impersonationToken);

        // Identitas SUDAH sepenuhnya milik target TenantAdmin - endpoint TenantAdminOnly berhasil...
        var schoolUsersResp = await asTargetClient.GetAsync("/api/school-users");
        Assert.Equal(HttpStatusCode.OK, schoolUsersResp.StatusCode);

        // ...dan endpoint SaOnly (yang tadinya bisa diakses SA ASLI) sekarang 403 - bukti identitas
        // TERTUKAR PENUH, bukan sekadar SA "mengintip" dgn hak aksesnya sendiri tetap menempel.
        var saOnlyResp = await asTargetClient.GetAsync("/sa/tenants");
        Assert.Equal(HttpStatusCode.Forbidden, saOnlyResp.StatusCode);

        using var scope = _factory.CreateDbScope();
        var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
        var startLog = await db.AuditLogs.AsNoTracking()
            .Where(a => a.Action == "ImpersonationStarted" && a.EntityId == target.Id.ToString())
            .OrderByDescending(a => a.CreatedAt).FirstOrDefaultAsync();
        Assert.NotNull(startLog);
        Assert.Equal(saUser.Id, startLog!.ActorUserId);
        Assert.Equal(target.Id, startLog.ActingAsUserId);
    }

    [Fact]
    public async Task DuringImpersonation_AuditWriteByAnyEndpoint_RecordsRealActorAutomatically()
    {
        var tenant = await _factory.SeedTenantAsync();
        var (saUser, saClient) = await _factory.LoginAsAsync(UserRole.SuperAdmin, null, "impersonate-sa-audit");
        var target = await Vokasia.Tests.Auth.AuthTestHelpers.SeedUserAsync(_factory, "impersonate-target-audit", UserRole.TenantAdmin, tenant.Id);

        var startResp = await StartImpersonationAsync(saClient, target.Id);
        var tokenJson = await startResp.Content.ReadFromJsonAsync<JsonElement>();
        var asTargetClient = AnonClientWithBearer(tokenJson.GetProperty("access_token").GetString()!);

        // EndImpersonation sendiri menulis AuditLog via ITenantContext.UserId (= target saat itu) TANPA
        // tahu apa pun soal SA - VokasiaDbContext.SaveChangesAsync-lah yang mengoreksinya, membuktikan
        // "satu pintu" bekerja utk endpoint APA PUN yang menulis AuditLog, bukan cuma StartImpersonation
        // yang MEMANG sudah tahu (ActingAsUserId diisi eksplisit).
        var endResp = await asTargetClient.PostAsync("/api/impersonation/end", content: null);
        Assert.Equal(HttpStatusCode.NoContent, endResp.StatusCode);

        using var scope = _factory.CreateDbScope();
        var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
        var endLog = await db.AuditLogs.AsNoTracking()
            .Where(a => a.Action == "ImpersonationEnded" && a.EntityId == target.Id.ToString())
            .OrderByDescending(a => a.CreatedAt).FirstOrDefaultAsync();
        Assert.NotNull(endLog);
        Assert.Equal(saUser.Id, endLog!.ActorUserId);
        Assert.Equal(target.Id, endLog.ActingAsUserId);
    }

    [Fact]
    public async Task StartImpersonation_TargetIsSuperAdmin_Rejected()
    {
        var (_, saClient) = await _factory.LoginAsAsync(UserRole.SuperAdmin, null, "impersonate-sa-vs-sa");
        var otherSa = await Vokasia.Tests.Auth.AuthTestHelpers.SeedUserAsync(_factory, "impersonate-target-sa", UserRole.SuperAdmin, null);

        var resp = await StartImpersonationAsync(saClient, otherSa.Id);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_grant", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task StartImpersonation_CallerNotSuperAdmin_Rejected()
    {
        var tenant = await _factory.SeedTenantAsync();
        var (_, teacherClient) = await _factory.LoginAsAsync(UserRole.Teacher, tenant.Id, "impersonate-non-sa-caller");
        var target = await Vokasia.Tests.Auth.AuthTestHelpers.SeedUserAsync(_factory, "impersonate-target-2", UserRole.TenantAdmin, tenant.Id);

        var resp = await StartImpersonationAsync(teacherClient, target.Id);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_grant", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task EndImpersonation_WithoutActiveImpersonation_Returns400()
    {
        var tenant = await _factory.SeedTenantAsync();
        var (_, teacherClient) = await _factory.LoginAsAsync(UserRole.Teacher, tenant.Id, "end-impersonation-no-op");

        var resp = await teacherClient.PostAsync("/api/impersonation/end", content: null);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
