using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Vokasia.Api.Auth;
using Vokasia.Domain.Common;
using Vokasia.Tests.Auth;

namespace Vokasia.Tests.Security;

/// <summary>
/// AC VOK-H2-E3 §4 RefreshRotationTests ("reuse refresh lama -> seluruh keluarga sesi tercabut")
/// — slice yang ditulis menyusul. Test ini membuktikan PRASYARAT backend yang jadi fondasi seluruh
/// mekanisme reuse-detection: OpenIddict (<c>UseReferenceRefreshTokens</c>, OpenIddictSetup.cs)
/// BENAR-BENAR menolak refresh token yang sudah dipakai sekali utk rotasi (bukan cuma diasumsikan
/// dari baca konfigurasi — pola PROMPT D sesi ini: buktikan lewat request sungguhan).
///
/// "Cabut SELURUH KELUARGA sesi" itu sendiri adalah tanggung jawab lapisan BFF/Redis
/// (<c>frontend/src/lib/refresh.ts</c>: <c>refreshOnExpiry -&gt; revokeAllSessionsForUser</c>, KODE
/// SUDAH ADA sejak slice H2-E3 sebelumnya) yang bereaksi thd sinyal <c>invalid_grant</c> yang
/// dibuktikan di sini — bagian "cabut keluarga" perlu test Next.js/Redis tersendiri (di luar
/// jangkauan suite xUnit backend ini, TIDAK diklaim teruji di sini).
///
/// Beda dari <c>BearerAuthenticationTest.RefreshToken_AfterRevocation_IsRejected</c> (menguji jalur
/// LOGOUT eksplisit lewat <c>/connect/revoke</c>): di sini TANPA revoke eksplisit sama sekali —
/// token lama ditolak semata-mata krn sudah "dipakai" utk rotasi (dipertukarkan ke refresh token
/// baru) — skenario pencurian token (attacker replay refresh lama setelah korban sudah lanjut pakai
/// yang baru), bukan skenario logout sadar.
/// </summary>
public class RefreshRotationTests : IClassFixture<VokasiaApiFactory>
{
    private readonly VokasiaApiFactory _factory;
    public RefreshRotationTests(VokasiaApiFactory factory) => _factory = factory;

    private static async Task<HttpResponseMessage> RefreshAsync(HttpClient client, string refreshToken) =>
        await client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = OpenIddictSetup.BffClientId,
            ["client_secret"] = AuthTestHelpers.ClientSecret,
        }));

    [Fact]
    public async Task OldRefreshToken_AfterRotation_IsRejected()
    {
        var user = await AuthTestHelpers.SeedUserAsync(_factory, "rotate", UserRole.TenantAdmin, Guid.NewGuid());
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var (_, refreshTokenA) = await AuthTestHelpers.LoginAndExchangeAsync(client, user.Email!);
        Assert.False(string.IsNullOrEmpty(refreshTokenA));

        // Rotasi pertama: A -> B. Harus sukses (belum pernah dipakai).
        var firstRotate = await RefreshAsync(client, refreshTokenA!);
        Assert.Equal(HttpStatusCode.OK, firstRotate.StatusCode);
        var firstJson = await firstRotate.Content.ReadFromJsonAsync<JsonElement>();
        var refreshTokenB = firstJson.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;
        Assert.False(string.IsNullOrEmpty(refreshTokenB));
        Assert.NotEqual(refreshTokenA, refreshTokenB); // token BARU, bukan yang lama dipakai ulang.

        // Reuse A (sudah "dipakai" utk dapat B) -> HARUS ditolak — ini prasyarat reuse-detection.
        var reuseAttempt = await RefreshAsync(client, refreshTokenA!);
        Assert.Equal(HttpStatusCode.BadRequest, reuseAttempt.StatusCode); // invalid_grant.
    }

    [Fact]
    public async Task NewRefreshToken_AfterRotation_StillWorks()
    {
        // Sanity companion: rotasi BUKAN "matikan semua sesi", cuma yang lama. B tetap valid dipakai.
        var user = await AuthTestHelpers.SeedUserAsync(_factory, "rotate2", UserRole.TenantAdmin, Guid.NewGuid());
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var (_, refreshTokenA) = await AuthTestHelpers.LoginAndExchangeAsync(client, user.Email!);
        var firstRotate = await RefreshAsync(client, refreshTokenA!);
        var firstJson = await firstRotate.Content.ReadFromJsonAsync<JsonElement>();
        var refreshTokenB = firstJson.GetProperty("refresh_token").GetString()!;

        var secondRotate = await RefreshAsync(client, refreshTokenB);

        Assert.Equal(HttpStatusCode.OK, secondRotate.StatusCode);
    }
}
