using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Testing;
using Vokasia.Domain.Common;

namespace Vokasia.Tests.Auth;

/// <summary>
/// AC VOK-H2-E3 (DECISIONS.md D17): regresi utk bug "DefaultAuthenticationScheme = Cookies"
/// (<c>IdentitySetup.cs</c>, sebelum sesi ini <c>AddAuthentication(CookieAuthenticationDefaults
/// .AuthenticationScheme)</c>) yang membuat SEMUA endpoint resource
/// (<c>RequireAuthorization(RbacPolicies.*)</c> tanpa <c>AddAuthenticationSchemes</c> eksplisit)
/// diam-diam diautentikasi lewat handler Cookie SAJA — Bearer token JWT yang VALID tetap dibalas
/// 302 ke <c>/account/login</c> (perilaku "challenge" Cookie thd request tanpa cookie), bukan
/// otentikasi normal. Ketahuan pertama kali lewat smoke test HTTP nyata (curl+PowerShell, di luar
/// suite ini), BUKAN test — celah persis yang PROMPT D template.md minta ditutup ("ubah 1 logika
/// inti -> test harus merah"). Test ini menjalankan flow OAuth code+PKCE PENUH lewat HttpClient
/// TestServer in-process (cookie otomatis via WebApplicationFactoryClientOptions.HandleCookies
/// default true) sampai memanggil endpoint resource sungguhan dgn Bearer token hasil exchange —
/// sengaja end-to-end (bukan cuma assert DefaultScheme di options) krn bug aslinya HANYA muncul
/// lewat request Bearer sungguhan, tidak lewat pengecekan konfigurasi statis.
///
/// SeedUserAsync/LoginAndExchangeAsync dipindah ke <see cref="AuthTestHelpers"/> (VOK-H2-E3 §4,
/// slice ditulis menyusul) saat RbacPolicyTests/RefreshRotationTests butuh persis logika yang
/// sama — lihat doc-comment di sana.
/// </summary>
public class BearerAuthenticationTest : IClassFixture<VokasiaApiFactory>
{
    private readonly VokasiaApiFactory _factory;

    public BearerAuthenticationTest(VokasiaApiFactory factory) => _factory = factory;

    [Fact]
    public async Task ProtectedResourceEndpoint_WithValidBearerToken_ReturnsOk_NotLoginRedirect()
    {
        var user = await AuthTestHelpers.SeedUserAsync(_factory, "resource", UserRole.TenantAdmin, Guid.NewGuid());
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var (accessToken, _) = await AuthTestHelpers.LoginAndExchangeAsync(client, user.Email!);

        // Access token HARUS berupa JWS 3-segmen (DisableAccessTokenEncryption, DECISIONS.md D17) - BFF/test bisa baca isinya.
        Assert.Equal(3, accessToken.Split('.').Length);

        var apiReq = new HttpRequestMessage(HttpMethod.Get, "/api/periods?pageSize=1");
        apiReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var apiResp = await client.SendAsync(apiReq);

        // Sebelum fix D17: ini 302 Found ke /account/login (default scheme Cookies "menelan" Bearer request).
        Assert.Equal(HttpStatusCode.OK, apiResp.StatusCode);
    }

    [Fact]
    public async Task ProtectedResourceEndpoint_WithNoToken_IsRejected_NotSilentlyAllowed()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var apiResp = await client.GetAsync("/api/periods?pageSize=1");

        // Redirect (Cookie challenge) ATAU 401 keduanya sah sbg "ditolak"; yang TIDAK boleh adalah 200.
        Assert.NotEqual(HttpStatusCode.OK, apiResp.StatusCode);
    }

    [Fact]
    public async Task RefreshToken_AfterRevocation_IsRejected()
    {
        // AC VOK-H2-E3 §2/§4 RevocationTests (separuh "logout"): FR-AUTH-04 "logout -> refresh dicabut instan".
        // Separuh "deactivate" ada di Security/RevocationTests.cs (ditulis menyusul, lihat doc-comment di sana).
        var user = await AuthTestHelpers.SeedUserAsync(_factory, "revoke", UserRole.TenantAdmin, Guid.NewGuid());
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var (_, refreshToken) = await AuthTestHelpers.LoginAndExchangeAsync(client, user.Email!);
        Assert.False(string.IsNullOrEmpty(refreshToken));

        var revokeForm = new Dictionary<string, string>
        {
            ["token"] = refreshToken!,
            ["token_type_hint"] = "refresh_token",
            ["client_id"] = Vokasia.Api.Auth.OpenIddictSetup.BffClientId,
            ["client_secret"] = AuthTestHelpers.ClientSecret,
        };
        var revokeResp = await client.PostAsync("/connect/revoke", new FormUrlEncodedContent(revokeForm));
        Assert.Equal(HttpStatusCode.OK, revokeResp.StatusCode);

        var refreshForm = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken!,
            ["client_id"] = Vokasia.Api.Auth.OpenIddictSetup.BffClientId,
            ["client_secret"] = AuthTestHelpers.ClientSecret,
        };
        var refreshResp = await client.PostAsync("/connect/token", new FormUrlEncodedContent(refreshForm));

        Assert.Equal(HttpStatusCode.BadRequest, refreshResp.StatusCode); // invalid_grant - token sudah dicabut.
    }
}
