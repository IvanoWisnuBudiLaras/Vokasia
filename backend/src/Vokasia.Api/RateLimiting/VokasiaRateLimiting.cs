using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Vokasia.Api.RateLimiting;

/// <summary>
/// VOK-H3-E3 §3. Rate limiter BAWAAN ASP.NET Core (Microsoft.AspNetCore.RateLimiting, shared
/// framework net10.0 — TANPA paket NuGet baru, sesuai larangan ticket "menambah dependency baru").
/// Dua policy:
///  - "login" (5/mnt, sliding window, partisi per IP+email) — dipasang di POST /account/login.
///  - "public" (10/mnt, sliding window, partisi per IP) — dipasang di endpoint anonim/token-exchange.
///
/// [DEVIASI dicatat, bukan diam-diam — DECISIONS.md D25]: ticket §3 menulis "/connect/token
/// (password/code grant)" sbg tempat policy "login" terpasang. Proyek ini TIDAK PERNAH
/// mengimplementasikan password grant di /connect/token (lihat AuthorizationController.Exchange():
/// hanya authorization_code, refresh_token, dan grant kustom magic-link — SEMUANYA menukar
/// KREDENSIAL BER-ENTROPI TINGGI yang sudah diterbitkan sebelumnya, bukan password pendek yang bisa
/// ditebak). Password SUNGGUHAN disubmit di POST /account/login (AccountEndpoints.cs, form
/// email+password, H1-E3) — ITULAH permukaan brute-force nyata yang dimaksud AC "percobaan login".
/// Maka policy "login" (ketat, per-identitas) dipasang di /account/login (partisi IP+email persis
/// spt ticket minta); /connect/token dipasang policy "public" (IP saja, lebih longgar) — cukup utk
/// menahan automated abuse thd exchange token tanpa salah sasaran menganggap SEMUA request ke
/// endpoint yang dipakai bersama oleh banyak grant (termasuk refresh rutin BFF) sbg "1 percobaan
/// login". Ini JUGA sekaligus menghindari isu sungguhan: banyak test suite (RbacPolicyTests dkk)
/// login sbg *banyak user berbeda* lewat SATU factory/IP bersama dlm 1 test class — kalau /connect/token
/// dibatasi ketat per-IP TANPA identitas, test yg sah pun akan false-positive kena 429 (dibuktikan
/// nyata: dicoba, memang gagal, baru direvisi ke desain ini — bukan diasumsikan aman dari awal).
///
/// Partisi "login" butuh field "email" dari FORM BODY /account/login — dibaca lewat
/// <c>httpContext.Request.Form["email"]</c> DI DALAM partition-key selector yang SINKRON. Ini aman
/// HANYA karena Program.cs memasang middleware <c>ReadFormAsync()</c> lebih dulu (sebelum
/// UseRateLimiter()) utk request ber-form-content-type — tanpa itu, akses .Form sinkron thd body
/// yang belum pernah dibaca BISA throw "Synchronous operations are disallowed" di Kestrel produksi
/// (AllowSynchronousIO=false default — beda dari TestServer yg lebih permisif, kelas gap yg
/// berulang kali ditemukan sesi ini: lolos test tapi gagal di deployment nyata). Form yang sudah
/// di-cache middleware itu aman dibaca ulang (sync maupun async) tanpa re-read stream — PostLogin
/// sendiri (AccountEndpoints.cs) tetap memanggil ReadFormAsync() lagi, mengambil dari cache yang sama.
/// </summary>
public static class VokasiaRateLimiting
{
    public const string LoginPolicy = "login";
    public const string PublicPolicy = "public";
    private const int LoginAttemptsPerIdentity = 5;
    private const int DefaultLoginAttemptsPerIp = 20;

    public static IServiceCollection AddVokasiaRateLimiting(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        var loginAttemptsPerIp = int.TryParse(
            configuration?["RateLimiting:LoginAttemptsPerIp"],
            out var configuredLoginAttemptsPerIp) && configuredLoginAttemptsPerIp > 0
            ? configuredLoginAttemptsPerIp
            : DefaultLoginAttemptsPerIp;

        services.AddRateLimiter(options =>
        {
            // Endpoint metadata hanya membawa satu named policy. Limiter IP dipasang sebagai
            // global limiter yang hanya aktif pada POST /account/login, lalu policy "login"
            // tetap membatasi partisi IP+email. Keduanya harus lolos pada setiap percobaan.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                if (!httpContext.Request.Path.Equals("/account/login", StringComparison.OrdinalIgnoreCase) ||
                    !HttpMethods.IsPost(httpContext.Request.Method))
                {
                    return RateLimitPartition.GetNoLimiter<string>("not-login");
                }

                var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetSlidingWindowLimiter($"login-ip:{ip}", _ => new SlidingWindowRateLimiterOptions
                {
                    Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 4,
                    PermitLimit = loginAttemptsPerIp,
                    QueueLimit = 0,
                    AutoReplenishment = true,
                });
            });

            // AC ticket §3 & §Acceptance Criteria: "Given 429, Then body & header Retry-After konsisten."
            options.OnRejected = async (context, ct) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                var retryAfterSeconds = 60;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    retryAfterSeconds = (int)Math.Ceiling(retryAfter.TotalSeconds);
                }

                context.HttpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString();
                await context.HttpContext.Response.WriteAsJsonAsync(
                    new { code = "rate-limit-exceeded", message = "Terlalu banyak percobaan. Coba lagi nanti." },
                    ct);
            };

            options.AddPolicy(LoginPolicy, httpContext =>
            {
                // Form SUDAH di-cache middleware pre-buffer (Program.cs) - akses sync di sini aman.
                // Email kosong (mis. request tanpa body form sama sekali) tetap terpartisi per-IP.
                var email = httpContext.Request.HasFormContentType
                    ? httpContext.Request.Form["email"].ToString()
                    : string.Empty;
                var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var key = $"login:{ip}:{email.ToLowerInvariant()}";

                return RateLimitPartition.GetSlidingWindowLimiter(key, _ => new SlidingWindowRateLimiterOptions
                {
                    Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 4,
                    PermitLimit = LoginAttemptsPerIdentity,
                    QueueLimit = 0,
                    AutoReplenishment = true,
                });
            });

            options.AddPolicy(PublicPolicy, httpContext =>
            {
                var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var key = $"public:{ip}";

                return RateLimitPartition.GetSlidingWindowLimiter(key, _ => new SlidingWindowRateLimiterOptions
                {
                    Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 4,
                    PermitLimit = 10,
                    QueueLimit = 0,
                    AutoReplenishment = true,
                });
            });
        });

        return services;
    }
}
