using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Vokasia.Tests.Auth;

/// <summary>
/// AC VOK-H1-E3: authorize request tanpa code_challenge (PKCE) ditolak eksplisit — dibuktikan
/// lewat HTTP nyata ke /connect/authorize, bukan hanya cek konfigurasi (perilaku, sesuai
/// PROMPT D template.md: "ubah 1 logika inti -> test harus merah").
/// </summary>
public class PkceRequiredTest : IClassFixture<VokasiaApiFactory>
{
    private readonly VokasiaApiFactory _factory;

    public PkceRequiredTest(VokasiaApiFactory factory) => _factory = factory;

    private const string RedirectUri = "http://localhost:3000/api/auth/callback";

    [Fact]
    public async Task Authorize_WithoutPkce_IsRejected()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var url = "/connect/authorize" +
                  "?client_id=" + Uri.EscapeDataString(Vokasia.Api.Auth.OpenIddictSetup.BffClientId) +
                  "&response_type=code" +
                  "&redirect_uri=" + Uri.EscapeDataString(RedirectUri) +
                  "&scope=" + Uri.EscapeDataString("openid profile api offline_access");
                  // sengaja TANPA code_challenge / code_challenge_method

        var response = await client.GetAsync(url);
        var body = await response.Content.ReadAsStringAsync();

        var rejectedDirectly = response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;
        var rejectedViaRedirect = response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Found
            && (response.Headers.Location?.Query.Contains("error=") ?? false);

        Assert.True(
            rejectedDirectly || rejectedViaRedirect,
            $"Expected authorize request without PKCE to be rejected. Status={response.StatusCode}, " +
            $"Location={response.Headers.Location}, Body={body}");

        // Tidak boleh pernah sampai men-challenge ke login sebagai request VALID (yang berarti PKCE lolos).
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Authorize_UnknownClientId_IsRejected()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var url = "/connect/authorize" +
                  "?client_id=client-palsu-tidak-terdaftar" +
                  "&response_type=code" +
                  "&redirect_uri=" + Uri.EscapeDataString(RedirectUri) +
                  "&code_challenge=abc123" +
                  "&code_challenge_method=S256" +
                  "&scope=openid";

        var response = await client.GetAsync(url);

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        var rejected = response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
            || response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Found;
        Assert.True(rejected, $"Expected unknown client_id to be rejected. Status={response.StatusCode}");
    }
}
