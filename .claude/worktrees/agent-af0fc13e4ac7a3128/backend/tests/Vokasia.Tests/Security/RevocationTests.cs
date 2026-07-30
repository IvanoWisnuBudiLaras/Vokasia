using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Vokasia.Api.Auth;
using Vokasia.Domain.Common;
using Vokasia.Infrastructure.Identity;
using Vokasia.Tests.Auth;

namespace Vokasia.Tests.Security;

/// <summary>
/// AC VOK-H2-E3 §4 RevocationTests ("logout/deactivate -> request berikutnya 401 instan") — slice
/// yang ditulis menyusul. Separuh "logout" (via <c>/connect/revoke</c>) SUDAH ada di
/// <c>BearerAuthenticationTest.RefreshToken_AfterRevocation_IsRejected</c> (ditulis slice
/// sebelumnya, SENGAJA tidak diduplikasi di sini). File ini melengkapi separuh "deactivate" yang
/// belum diuji sama sekali.
///
/// Catatan arsitektur JUJUR (bukan klaim berlebihan): "instan" di sini berarti instan pada TITIK
/// LOGIN dan REFRESH BERIKUTNYA (keduanya sudah eksplisit cek <c>user.IsActive</c> —
/// <c>AccountEndpoints.PostLogin</c> & <c>AuthorizationController.Exchange</c> grant
/// refresh_token). Access token yang SUDAH terbit sebelum deaktivasi TETAP valid sampai masa
/// berlakunya habis (15 menit, <c>OpenIddictSetup.SetAccessTokenLifetime</c>) — resource endpoint
/// memvalidasi JWT (tanda tangan+exp) secara lokal, TIDAK query DB per request. Ini trade-off
/// desain SADAR (window eksposur dibatasi oleh lifetime pendek, bukan dicek live tiap request),
/// bukan celah tak disadari — didokumentasikan eksplisit di sini drpd diam-diam mengklaim "401
/// instan" utk access token yang masih berlaku.
/// </summary>
public class RevocationTests : IClassFixture<VokasiaApiFactory>
{
    private readonly VokasiaApiFactory _factory;
    public RevocationTests(VokasiaApiFactory factory) => _factory = factory;

    [Fact]
    public async Task DeactivatedUser_CannotLogin()
    {
        var user = await AuthTestHelpers.SeedUserAsync(_factory, "nonaktif-login", UserRole.TenantAdmin, Guid.NewGuid(), isActive: false);
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var resp = await AuthTestHelpers.AttemptLoginFormAsync(client, user.Email!);

        // AccountEndpoints.PostLogin: user tak aktif -> redirect BALIK ke /account/login?error=...
        // (303), BUKAN 303 ke returnUrl asli (yang berarti login sukses).
        Assert.Equal(HttpStatusCode.SeeOther, resp.StatusCode);
        var location = resp.Headers.Location!.ToString();
        Assert.Contains("/account/login", location);
        Assert.Contains("error=", location);
    }

    [Fact]
    public async Task DeactivatedAfterLogin_CannotRefresh()
    {
        var user = await AuthTestHelpers.SeedUserAsync(_factory, "nonaktif-refresh", UserRole.TenantAdmin, Guid.NewGuid());
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var (_, refreshToken) = await AuthTestHelpers.LoginAndExchangeAsync(client, user.Email!);
        Assert.False(string.IsNullOrEmpty(refreshToken));

        // Deaktivasi SETELAH login berhasil (skenario nyata: admin nonaktifkan akun bermasalah
        // sementara sesi lama user itu masih membawa refresh token yang valid).
        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var freshUser = await userManager.FindByIdAsync(user.Id.ToString());
            freshUser!.IsActive = false;
            await userManager.UpdateAsync(freshUser);
        }

        var refreshForm = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken!,
            ["client_id"] = OpenIddictSetup.BffClientId,
            ["client_secret"] = AuthTestHelpers.ClientSecret,
        };
        var refreshResp = await client.PostAsync("/connect/token", new FormUrlEncodedContent(refreshForm));

        // AuthorizationController.Exchange (grant refresh_token): !user.IsActive -> Forbid. Cek ini
        // SUDAH ADA di kode sejak awal tapi belum pernah dibuktikan lewat HTTP nyata sampai test ini.
        Assert.Equal(HttpStatusCode.BadRequest, refreshResp.StatusCode);
    }
}
