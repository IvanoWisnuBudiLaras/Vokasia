using System.Net;
using System.Net.Http.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc.Testing;
using Vokasia.Tests.Auth;

namespace Vokasia.Tests.Guard;

/// <summary>
/// AC VOK-H3-E3 §4 RateLimitTests: login ke-6 dalam 1 mnt -> 429; setelah jendela -> pulih.
///
/// "Setelah jendela -> pulih" diuji thd System.Threading.RateLimiting.SlidingWindowRateLimiter
/// LANGSUNG dgn window PENDEK (bukan thd policy "login" 1-mnt sungguhan lewat HTTP) - primitif yang
/// SAMA PERSIS dipakai VokasiaRateLimiting.cs (RateLimitPartition.GetSlidingWindowLimiter membungkus
/// kelas ini), jadi membuktikan algoritmanya "pulih setelah window" tanpa test harus tidur 60+ detik
/// nyata (memperlambat SETIAP dotnet test berikutnya secara permanen demi 1 assertion).
/// </summary>
public class RateLimitTests : IClassFixture<VokasiaApiFactory>
{
    private readonly VokasiaApiFactory _factory;
    public RateLimitTests(VokasiaApiFactory factory) => _factory = factory;

    private static FormUrlEncodedContent LoginForm(string email) => new(new Dictionary<string, string>
    {
        ["email"] = email,
        ["password"] = "salah-sengaja-tidak-penting",
        ["returnUrl"] = "/",
    });

    [Fact]
    public async Task PostAccountLogin_SixthAttemptSameEmailWithinOneMinute_Returns429WithRetryAfter()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var email = $"brute-force-{Guid.NewGuid():N}@vokasia.test"; // 1 email = 1 partisi (IP+email) - tak diganggu test lain di kelas ini.

        HttpResponseMessage? last = null;
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            last = await client.PostAsync("/account/login", LoginForm(email));
            Assert.NotEqual(HttpStatusCode.TooManyRequests, last.StatusCode); // 5 percobaan pertama BELUM kena limit (credential salah -> 303, itu wajar & bukan concern test ini).
        }

        var sixth = await client.PostAsync("/account/login", LoginForm(email));

        Assert.Equal(HttpStatusCode.TooManyRequests, sixth.StatusCode);
        Assert.True(sixth.Headers.RetryAfter is not null || sixth.Headers.Contains("Retry-After"), "Header Retry-After wajib ada di respons 429 (AC ticket §4).");
        var body = await sixth.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("rate-limit-exceeded", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task PostAccountLogin_DifferentEmailsSameClient_AreNotThrottledByEachOther()
    {
        // Membuktikan partisi IP+EMAIL (bukan IP saja) - 8 email BERBEDA dari 1 client/IP yang sama,
        // TIDAK SATU PUN boleh kena 429 walau totalnya > limit 5, krn masing2 partisi sendiri-sendiri.
        // Ini jugalah alasan RbacPolicyTests dkk (banyak login user berbeda dari 1 factory) tetap aman.
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        for (var i = 0; i < 8; i++)
        {
            var email = $"user-{i}-{Guid.NewGuid():N}@vokasia.test";
            var resp = await client.PostAsync("/account/login", LoginForm(email));
            Assert.NotEqual(HttpStatusCode.TooManyRequests, resp.StatusCode);
        }
    }

    [Fact]
    public async Task SlidingWindowRateLimiter_RecoversAfterWindowElapses()
    {
        // Primitif SAMA PERSIS yg dipakai VokasiaRateLimiting.cs (window dipendekkan drastis khusus
        // test ini, BUKAN mengubah konfigurasi produksi 1 mnt) - membuktikan "pulih setelah jendela"
        // scr nyata & cepat (<1 detik), bukan diasumsikan dari baca dokumentasi library semata.
        using var limiter = new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
        {
            Window = TimeSpan.FromMilliseconds(300),
            SegmentsPerWindow = 3,
            PermitLimit = 2,
            QueueLimit = 0,
            AutoReplenishment = true,
        });

        using (var l1 = await limiter.AcquireAsync(1)) Assert.True(l1.IsAcquired);
        using (var l2 = await limiter.AcquireAsync(1)) Assert.True(l2.IsAcquired);
        using (var l3 = await limiter.AcquireAsync(1)) Assert.False(l3.IsAcquired); // limit 2 - percobaan ke-3 ditolak.

        await Task.Delay(500); // > Window (300ms) + toleransi replenishment.

        using var afterRecovery = await limiter.AcquireAsync(1);
        Assert.True(afterRecovery.IsAcquired); // AC: "setelah jendela -> pulih".
    }
}
