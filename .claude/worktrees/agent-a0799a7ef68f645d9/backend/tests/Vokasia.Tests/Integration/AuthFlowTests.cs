using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Vokasia.Domain.Common;
using Vokasia.Tests.Auth;

namespace Vokasia.Tests.Integration;

/// <summary>
/// VOK-H5-E3 §1 AuthFlowTests — code+PKCE PENUH → BFF exchange (/connect/token) → panggil API
/// ber-Bearer; refresh_token grant → access token BARU → panggil API tetap sukses. NFR-SEC-01.
///
/// Suite ini SENGAJA mengulang skenario yang sudah dibuktikan Auth/BearerAuthenticationTest.cs
/// (InMemory) - di sini terhadap Postgres Testcontainers SUNGGUHAN (OpenIddict token/authorization
/// store adalah baris tabel relasional nyata - lihat migrasi OpenIddictTokens/OpenIddictAuthorizations,
/// bukan sekadar Dictionary in-memory) - inilah beda substansial yang jadi alasan ticket ini ada.
///
/// "Token expired → refresh" ditafsirkan LEWAT MEKANISME refresh_token grant itu sendiri (memperoleh
/// access token BARU dan membuktikannya valid dipakai) - BUKAN menunggu 900 detik expiry sungguhan
/// atau memundurkan jam sistem (keduanya di luar kendali test yang deterministik/cepat) - AC
/// substansi "refresh -> sukses" tetap dibuktikan penuh: refresh token yang sama menghasilkan access
/// token baru yang tetap diterima endpoint terproteksi.
/// </summary>
[Collection("IntegrationTests")]
public class AuthFlowTests
{
    private readonly VokasiaIntegrationFactory _factory;
    public AuthFlowTests(VokasiaIntegrationFactory factory) => _factory = factory;

    [Fact]
    public async Task FullPkceCodeFlow_ThenCallProtectedEndpointWithBearer_Succeeds()
    {
        var user = await AuthTestHelpers.SeedUserAsync(_factory, "authflow-full", UserRole.TenantAdmin, Guid.NewGuid());
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var (accessToken, refreshToken) = await AuthTestHelpers.LoginAndExchangeAsync(client, user.Email!);

        Assert.Equal(3, accessToken.Split('.').Length); // JWS 3-segmen (DisableAccessTokenEncryption, D17).
        Assert.False(string.IsNullOrEmpty(refreshToken));

        var req = new HttpRequestMessage(HttpMethod.Get, "/api/periods?pageSize=1");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task RefreshTokenGrant_ObtainsNewAccessToken_StillAcceptedByProtectedEndpoint()
    {
        var user = await AuthTestHelpers.SeedUserAsync(_factory, "authflow-refresh", UserRole.TenantAdmin, Guid.NewGuid());
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var (firstAccessToken, refreshToken) = await AuthTestHelpers.LoginAndExchangeAsync(client, user.Email!);
        Assert.False(string.IsNullOrEmpty(refreshToken));

        var refreshForm = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken!,
            ["client_id"] = Vokasia.Api.Auth.OpenIddictSetup.BffClientId,
            ["client_secret"] = AuthTestHelpers.ClientSecret,
        };
        var refreshResp = await client.PostAsync("/connect/token", new FormUrlEncodedContent(refreshForm));
        Assert.Equal(HttpStatusCode.OK, refreshResp.StatusCode);

        var refreshJson = await refreshResp.Content.ReadFromJsonAsync<JsonElement>();
        var newAccessToken = refreshJson.GetProperty("access_token").GetString()!;
        Assert.NotEqual(firstAccessToken, newAccessToken); // token BARU, bukan token lama dipakai ulang.

        var req = new HttpRequestMessage(HttpMethod.Get, "/api/periods?pageSize=1");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newAccessToken);
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_IsRejected()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var resp = await client.GetAsync("/api/periods?pageSize=1");
        Assert.NotEqual(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithTamperedBearerToken_IsRejected()
    {
        var user = await AuthTestHelpers.SeedUserAsync(_factory, "authflow-tamper", UserRole.TenantAdmin, Guid.NewGuid());
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var (accessToken, _) = await AuthTestHelpers.LoginAndExchangeAsync(client, user.Email!);

        // Ubah 1 karakter di signature segment - HARUS ditolak (bukan diam-diam diterima krn parsing longgar).
        var parts = accessToken.Split('.');
        var tamperedSignature = parts[2][..^1] + (parts[2][^1] == 'A' ? 'B' : 'A');
        var tampered = $"{parts[0]}.{parts[1]}.{tamperedSignature}";

        var req = new HttpRequestMessage(HttpMethod.Get, "/api/periods?pageSize=1");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tampered);
        var resp = await client.SendAsync(req);

        Assert.NotEqual(HttpStatusCode.OK, resp.StatusCode);
    }
}
