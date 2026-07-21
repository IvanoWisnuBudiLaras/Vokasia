using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Vokasia.Api.Auth;
using Vokasia.Domain.Common;
using Vokasia.Infrastructure.Identity;
using Xunit;

namespace Vokasia.Tests.Auth;

/// <summary>
/// Helper bersama utk suite Auth/Security yang butuh JWT sungguhan lewat dance code+PKCE PENUH
/// (bukan cuma cek konfigurasi statis — pelajaran D17: bug aslinya cuma muncul lewat request
/// sungguhan). Diekstrak dari <see cref="BearerAuthenticationTest"/> (yang sebelumnya menyimpan
/// <c>SeedUserAsync</c>/<c>LoginAndExchangeAsync</c> sbg private method) saat
/// RbacPolicyTests/RefreshRotationTests/RevocationTests (VOK-H2-E3 §4, slice yang ditulis
/// menyusul) butuh persis logika yang sama — drpd disalin ulang ke-3/4 kalinya (DRY, mencegah
/// drift antar salinan kalau flow OAuth berubah), diekstrak jadi satu titik.
/// </summary>
public static class AuthTestHelpers
{
    public const string RedirectUri = "http://localhost:3000/api/auth/callback";
    public const string ClientSecret = "dev-only-secret-change-me"; // cermin default OpenIddictSetup.cs bila OIDC_BFF_CLIENT_SECRET tak diset.
    public const string Password = "Password123!";

    public static (string Verifier, string Challenge) GeneratePkce()
    {
        var verifierBytes = RandomNumberGenerator.GetBytes(32);
        var verifier = Convert.ToBase64String(verifierBytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
        var challengeBytes = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        var challenge = Convert.ToBase64String(challengeBytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
        return (verifier, challenge);
    }

    public static async Task<AppUser> SeedUserAsync(
        VokasiaApiFactory factory, string emailLocalPart, UserRole role, Guid? tenantId, bool isActive = true)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var email = $"{emailLocalPart}-{Guid.NewGuid():N}@vokasia.test";

        var user = new AppUser
        {
            UserName = email,
            Email = email,
            FullName = "Test " + emailLocalPart,
            Role = role,
            TenantId = tenantId,
            IsActive = isActive,
        };
        var created = await userManager.CreateAsync(user, Password);
        Assert.True(created.Succeeded, string.Join(", ", created.Errors.Select(e => e.Description)));
        return user;
    }

    /// <summary>Jalankan seluruh dance code+PKCE (authorize -> login form -> authorize lagi -> token) dan kembalikan access_token + refresh_token.</summary>
    public static async Task<(string AccessToken, string? RefreshToken)> LoginAndExchangeAsync(
        HttpClient client, string email, string scope = "api offline_access")
    {
        var (verifier, challenge) = GeneratePkce();

        var authorizeUrl = "/connect/authorize" +
            "?client_id=" + Uri.EscapeDataString(OpenIddictSetup.BffClientId) +
            "&response_type=code" +
            "&redirect_uri=" + Uri.EscapeDataString(RedirectUri) +
            "&scope=" + Uri.EscapeDataString(scope) +
            "&state=teststate" +
            "&code_challenge=" + challenge +
            "&code_challenge_method=S256";

        var resp1 = await client.GetAsync(authorizeUrl);
        Assert.Equal(HttpStatusCode.Found, resp1.StatusCode);
        var loginLoc = resp1.Headers.Location!.ToString();
        Assert.StartsWith("/account/login", loginLoc);

        var loginQuery = QueryHelpers.ParseQuery(new Uri("http://test" + loginLoc).Query);
        var returnUrl = loginQuery["ReturnUrl"].ToString();

        var form = new Dictionary<string, string> { ["email"] = email, ["password"] = Password, ["returnUrl"] = returnUrl };
        var resp2 = await client.PostAsync("/account/login", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.SeeOther, resp2.StatusCode);

        var resp3 = await client.GetAsync(resp2.Headers.Location);
        Assert.Equal(HttpStatusCode.Found, resp3.StatusCode);
        var callbackLoc = resp3.Headers.Location!;
        var code = QueryHelpers.ParseQuery(callbackLoc.Query)["code"].ToString();
        Assert.False(string.IsNullOrEmpty(code));

        var tokenForm = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = RedirectUri,
            ["client_id"] = OpenIddictSetup.BffClientId,
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

    /// <summary>Coba login (authorize -> login form) TANPA mengasumsikan sukses — dipakai test yang justru mengharapkan penolakan (mis. user nonaktif).</summary>
    public static async Task<HttpResponseMessage> AttemptLoginFormAsync(HttpClient client, string email, string password = Password)
    {
        var (verifier, challenge) = GeneratePkce();
        var authorizeUrl = "/connect/authorize" +
            "?client_id=" + Uri.EscapeDataString(OpenIddictSetup.BffClientId) +
            "&response_type=code" +
            "&redirect_uri=" + Uri.EscapeDataString(RedirectUri) +
            "&scope=" + Uri.EscapeDataString("api offline_access") +
            "&state=teststate" +
            "&code_challenge=" + challenge +
            "&code_challenge_method=S256";

        var resp1 = await client.GetAsync(authorizeUrl);
        Assert.Equal(HttpStatusCode.Found, resp1.StatusCode);
        var loginLoc = resp1.Headers.Location!.ToString();
        var loginQuery = QueryHelpers.ParseQuery(new Uri("http://test" + loginLoc).Query);
        var returnUrl = loginQuery["ReturnUrl"].ToString();

        var form = new Dictionary<string, string> { ["email"] = email, ["password"] = password, ["returnUrl"] = returnUrl };
        return await client.PostAsync("/account/login", new FormUrlEncodedContent(form));
    }
}
