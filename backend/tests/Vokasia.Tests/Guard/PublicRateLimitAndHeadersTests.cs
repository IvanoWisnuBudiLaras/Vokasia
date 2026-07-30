using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Vokasia.Tests.Auth;

namespace Vokasia.Tests.Guard;

/// <summary>
/// VOK-H6-E3 §3 (NFR-SEC-07): (1) "verifikasi rate limit publik /p /verify 10/mnt" — AC ticket
/// literal minta pembuktian THD DUA RUTE KONKRET ini, bukan cuma policy "public" secara abstrak
/// (yang sudah diuji generik oleh VOK-H3-E3). (2) "security headers (CSP dasar, HSTS, nosniff)
/// terpasang, dibuktikan test" — SecurityHeadersMiddleware (Program.cs, paling awal pipeline).
///
/// [PENTING] VokasiaRateLimiting.PublicPolicy partisi HANYA per-IP ("public:{ip}", TANPA path) —
/// artinya SEMUA endpoint berkebijakan "public" berbagi SATU ember hitungan yang sama per IP.
/// TestServer selalu memakai IP loopback yang SAMA utk setiap request, jadi kalau 2 test dlm
/// kelas ini berbagi 1 WebApplicationFactory (spt pola IClassFixture di RateLimitTests.cs), test
/// KEDUA akan mewarisi sisa kuota test PERTAMA (bocor antar test, bukan lagi mengetes rute
/// spesifik). Maka SENGAJA TIDAK pakai IClassFixture di sini — tiap test method bikin
/// VokasiaApiFactory sendiri (murni in-process EF InMemory, murah) supaya limiter singleton-nya
/// betul2 nol-riwayat di awal setiap test.
/// </summary>
public class PublicRateLimitAndHeadersTests
{
    [Fact]
    public async Task GetPublicPortfolio_EleventhRequestSameIpWithinOneMinute_Returns429()
    {
        await using var factory = new VokasiaApiFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var slug = $"slug-tidak-ada-{Guid.NewGuid():N}"; // 404 tiap kali (portofolio tak pernah ada) - rate limiter tetap menghitung SEBELUM handler jalan, terlepas hasil endpoint.

        HttpResponseMessage? last = null;
        for (var i = 1; i <= 10; i++)
        {
            last = await client.GetAsync($"/p/{slug}");
            Assert.NotEqual(HttpStatusCode.TooManyRequests, last.StatusCode);
        }

        var eleventh = await client.GetAsync($"/p/{slug}");
        Assert.Equal(HttpStatusCode.TooManyRequests, eleventh.StatusCode);
    }

    [Fact]
    public async Task VerifyCertificate_EleventhRequestSameIpWithinOneMinute_Returns429()
    {
        await using var factory = new VokasiaApiFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var code = $"CERT-TIDAK-ADA-{Guid.NewGuid():N}";

        HttpResponseMessage? last = null;
        for (var i = 1; i <= 10; i++)
        {
            last = await client.GetAsync($"/api/verify/{code}");
            Assert.NotEqual(HttpStatusCode.TooManyRequests, last.StatusCode);
        }

        var eleventh = await client.GetAsync($"/api/verify/{code}");
        Assert.Equal(HttpStatusCode.TooManyRequests, eleventh.StatusCode);
    }

    [Theory]
    [InlineData("/health/ping")]
    [InlineData("/p/slug-apa-saja-utk-cek-header")]
    public async Task AnyResponse_CarriesBasicSecurityHeaders(string path)
    {
        await using var factory = new VokasiaApiFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var resp = await client.GetAsync(path);

        Assert.True(resp.Headers.TryGetValues("X-Content-Type-Options", out var nosniff));
        Assert.Equal("nosniff", nosniff!.Single());

        Assert.True(resp.Headers.TryGetValues("X-Frame-Options", out var frameOptions));
        Assert.Equal("DENY", frameOptions!.Single());

        Assert.True(resp.Headers.TryGetValues("Content-Security-Policy", out var csp));
        Assert.Contains("default-src 'none'", csp!.Single());

        Assert.True(resp.Headers.TryGetValues("Permissions-Policy", out var permissionsPolicy));
        Assert.Equal(
            "camera=(), microphone=(), geolocation=(), browsing-topics=()",
            permissionsPolicy!.Single());
    }
}
