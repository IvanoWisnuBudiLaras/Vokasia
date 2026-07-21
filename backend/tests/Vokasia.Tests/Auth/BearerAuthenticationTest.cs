using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Vokasia.Domain.Common;
using Vokasia.Infrastructure.Identity;

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
/// </summary>
public class BearerAuthenticationTest : IClassFixture<VokasiaApiFactory>
{
    private readonly VokasiaApiFactory _factory;

    public BearerAuthenticationTest(VokasiaApiFactory factory) => _factory = factory;

    private const string RedirectUri = "http://localhost:3000/api/auth/callback";
    private const string ClientSecret = "dev-only-secret-change-me"; // cermin default OpenIddictSetup.cs bila OIDC_BFF_CLIENT_SECRET tak diset.
    private const string Password = "Password123!";

    private static (string Verifier, string Challenge) GeneratePkce()
    {
        var verifierBytes = RandomNumberGenerator.GetBytes(32);
        var verifier = Convert.ToBase64String(verifierBytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
        var challengeBytes = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        var challenge = Convert.ToBase64String(challengeBytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
        return (verifier, challenge);
    }

    private async Task<AppUser> SeedUserAsync(string emailLocalPart, UserRole role, Guid? tenantId)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var email = $"{emailLocalPart}-{Guid.NewGuid():N}@vokasia.test";

        var user = new AppUser
        {
            UserName = email,
            Email = email,
            FullName = "Bearer Test " + emailLocalPart,
            Role = role,
            TenantId = tenantId,
            IsActive = true,
        };
        var created = await userManager.CreateAsync(user, Password);
        Assert.True(created.Succeeded, string.Join(", ", created.Errors.Select(e => e.Description)));
        return user;
    }

    /// <summary>Jalankan seluruh dance code+PKCE (authorize -> login form -> authorize lagi -> token) dan kembalikan access_token + refresh_token.</summary>
    private static async Task<(string AccessToken, string? RefreshToken)> LoginAndExchangeAsync(
        HttpClient client, string email, string scope = "api offline_access")
    {
        var (verifier, challenge) = GeneratePkce();

        var authorizeUrl = "/connect/authorize" +
            "?client_id=" + Uri.EscapeDataString(Vokasia.Api.Auth.OpenIddictSetup.BffClientId) +
            "&response_type=code" +
            "&redirect_uri=" + Uri.EscapeDataString(RedirectUri) +
            "&scope=" + Uri.EscapeDataString(scope) +
            "&state=teststate" +
            "&code_challenge=" + challenge +
            "&code_challenge_method=S256";

        // 1. Belum ada cookie auth -> AuthorizationController redirect manual ke /account/login (DECISIONS.md D17, gantikan Challenge() lama).
        var resp1 = await client.GetAsync(authorizeUrl);
        Assert.Equal(HttpStatusCode.Found, resp1.StatusCode);
        var loginLoc = resp1.Headers.Location!.ToString();
        Assert.StartsWith("/account/login", loginLoc);

        var loginQuery = QueryHelpers.ParseQuery(new Uri("http://test" + loginLoc).Query);
        var returnUrl = loginQuery["ReturnUrl"].ToString();

        // 2. POST kredensial ke /account/login (AccountEndpoints.cs, ditambah sesi ini krn gap H1-E3 -> DECISIONS.md D17).
        var form = new Dictionary<string, string> { ["email"] = email, ["password"] = Password, ["returnUrl"] = returnUrl };
        var resp2 = await client.PostAsync("/account/login", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.SeeOther, resp2.StatusCode); // 303 - AccountEndpoints.SeeOther (DECISIONS.md D17).

        // 3. GET returnUrl (sudah authenticated via cookie) -> 302 dgn ?code=...
        var resp3 = await client.GetAsync(resp2.Headers.Location);
        Assert.Equal(HttpStatusCode.Found, resp3.StatusCode);
        var callbackLoc = resp3.Headers.Location!;
        var code = QueryHelpers.ParseQuery(callbackLoc.Query)["code"].ToString();
        Assert.False(string.IsNullOrEmpty(code));

        // 4. Tukar code -> token (mirip frontend/src/app/api/auth/callback/route.ts).
        var tokenForm = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = RedirectUri,
            ["client_id"] = Vokasia.Api.Auth.OpenIddictSetup.BffClientId,
            ["client_secret"] = ClientSecret,
            ["code_verifier"] = verifier,
        };
        var tokenResp = await client.PostAsync("/connect/token", new FormUrlEncodedContent(tokenForm));
        Assert.Equal(HttpStatusCode.OK, tokenResp.StatusCode);
        var tokenJson = await tokenResp.Content.ReadFromJsonAsync<JsonElement>();

        var accessToken = tokenJson.GetProperty("access_token").GetString()!;
        var refreshToken = tokenJson.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;
        return (accessToken, refreshToken);
    }

    [Fact]
    public async Task ProtectedResourceEndpoint_WithValidBearerToken_ReturnsOk_NotLoginRedirect()
    {
        var user = await SeedUserAsync("resource", UserRole.TenantAdmin, Guid.NewGuid());
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var (accessToken, _) = await LoginAndExchangeAsync(client, user.Email!);

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
        // AC VOK-H2-E3 §2 RevocationTests: FR-AUTH-04 "logout -> refresh dicabut instan".
        var user = await SeedUserAsync("revoke", UserRole.TenantAdmin, Guid.NewGuid());
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var (_, refreshToken) = await LoginAndExchangeAsync(client, user.Email!);
        Assert.False(string.IsNullOrEmpty(refreshToken));

        var revokeForm = new Dictionary<string, string>
        {
            ["token"] = refreshToken!,
            ["token_type_hint"] = "refresh_token",
            ["client_id"] = Vokasia.Api.Auth.OpenIddictSetup.BffClientId,
            ["client_secret"] = ClientSecret,
        };
        var revokeResp = await client.PostAsync("/connect/revoke", new FormUrlEncodedContent(revokeForm));
        Assert.Equal(HttpStatusCode.OK, revokeResp.StatusCode);

        var refreshForm = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken!,
            ["client_id"] = Vokasia.Api.Auth.OpenIddictSetup.BffClientId,
            ["client_secret"] = ClientSecret,
        };
        var refreshResp = await client.PostAsync("/connect/token", new FormUrlEncodedContent(refreshForm));

        Assert.Equal(HttpStatusCode.BadRequest, refreshResp.StatusCode); // invalid_grant - token sudah dicabut.
    }
}
