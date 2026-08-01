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
                if (retryAfterSeconds <= 0) retryAfterSeconds = 60;

                context.HttpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString();

                var acceptHeader = context.HttpContext.Request.Headers.Accept.ToString();
                var isHtmlRequest = acceptHeader.Contains("text/html", StringComparison.OrdinalIgnoreCase);

                if (isHtmlRequest)
                {
                    context.HttpContext.Response.ContentType = "text/html; charset=utf-8";
                    await context.HttpContext.Response.WriteAsync(RenderRateLimitHtml(retryAfterSeconds), ct);
                }
                else
                {
                    await context.HttpContext.Response.WriteAsJsonAsync(
                        new { code = "rate-limit-exceeded", message = "Terlalu banyak percobaan. Coba lagi nanti." },
                        ct);
                }
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

    private static string RenderRateLimitHtml(int retryAfterSeconds)
    {
        return $$"""
            <!doctype html>
            <html lang="id">
            <head>
              <meta charset="utf-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover" />
              <meta name="color-scheme" content="light" />
              <title>Akses Dibatasi — Vokasia</title>
              <style>
                :root {
                  --color-surface: oklch(99% 0.009 94);
                  --color-surface-muted: oklch(96% 0.012 94);
                  --color-ink: oklch(18% 0.03 213);
                  --color-ink-muted: oklch(48% 0.03 213);
                  --color-border: oklch(65% 0.06 213);
                  --color-primary: oklch(50% 0.14 213);
                  --color-primary-hover: oklch(45% 0.14 213);
                  --color-primary-ink: oklch(99% 0.006 213);
                  --color-status-red: oklch(50% 0.16 25);
                  --color-status-red-bg: oklch(95% 0.03 25);
                  --font-display: "Geist", "Segoe UI", sans-serif;
                  --font-body: "Geist", "Segoe UI", sans-serif;
                  --radius-lg: 16px;
                  --radius-md: 10px;
                  --tap-min: 44px;
                }
                * { box-sizing: border-box; }
                body {
                  margin: 0;
                  padding: 20px;
                  font-family: var(--font-body);
                  background: var(--color-surface-muted);
                  color: var(--color-ink);
                  display: flex;
                  min-height: 100vh;
                  align-items: center;
                  justify-content: center;
                }
                .card {
                  background: var(--color-surface);
                  border: 1px solid var(--color-border);
                  border-radius: var(--radius-lg);
                  padding: 32px;
                  max-width: 440px;
                  width: 100%;
                  box-shadow: 0 4px 12px oklch(18% 0.03 213 / 0.08);
                  text-align: center;
                }
                .icon-wrapper {
                  display: inline-flex;
                  align-items: center;
                  justify-content: center;
                  width: 56px;
                  height: 56px;
                  border-radius: 50%;
                  background: var(--color-status-red-bg);
                  color: var(--color-status-red);
                  margin-bottom: 20px;
                  font-size: 28px;
                }
                h1 { font-size: 22px; font-weight: 700; margin: 0 0 10px 0; color: var(--color-ink); }
                p { font-size: 14px; color: var(--color-ink-muted); line-height: 1.6; margin: 0 0 20px 0; }
                .timer-box {
                  background: var(--color-status-red-bg);
                  border: 1px solid oklch(50% 0.16 25 / 0.3);
                  border-radius: var(--radius-md);
                  padding: 16px;
                  margin-bottom: 24px;
                }
                .timer-val {
                  font-size: 36px;
                  font-weight: 800;
                  color: var(--color-status-red);
                  font-family: monospace;
                }
                .timer-label {
                  font-size: 12px;
                  font-weight: 600;
                  color: var(--color-status-red);
                  margin-top: 4px;
                  text-transform: uppercase;
                  letter-spacing: 0.05em;
                }
                .btn {
                  display: inline-flex;
                  min-height: var(--tap-min);
                  width: 100%;
                  align-items: center;
                  justify-content: center;
                  background: var(--color-primary);
                  color: var(--color-primary-ink);
                  border-radius: var(--radius-md);
                  font-weight: 600;
                  text-decoration: none;
                  border: none;
                  font-size: 15px;
                  transition: background 120ms ease;
                }
                .btn:hover { background: var(--color-primary-hover); }
                .btn[disabled] { opacity: 0.5; pointer-events: none; }
              </style>
            </head>
            <body data-theme="sekolah">
              <main class="card">
                <div class="icon-wrapper">⚠️</div>
                <h1>Akses Dibatasi Sementara</h1>
                <p>Demi keamanan sistem (Design for Failure Rate Limiting), percobaan masuk dibatasi. Silakan tunggu hingga hitungan mundur selesai sebelum mencoba kembali.</p>

                <div class="timer-box">
                  <div class="timer-val" id="countdown">{{retryAfterSeconds}}s</div>
                  <div class="timer-label" id="status-label">Menunggu Pemulihan Akses</div>
                </div>

                <a href="/account/login" id="retry-btn" class="btn" disabled>Coba Masuk Kembali</a>
              </main>
              <script>
                (() => {
                  let seconds = {{retryAfterSeconds}};
                  const cd = document.getElementById("countdown");
                  const label = document.getElementById("status-label");
                  const btn = document.getElementById("retry-btn");
                  const timer = setInterval(() => {
                    seconds--;
                    if (seconds <= 0) {
                      clearInterval(timer);
                      if (cd) cd.textContent = "0s";
                      if (label) label.textContent = "Akses Siap Digunakan";
                      if (btn) btn.removeAttribute("disabled");
                    } else {
                      if (cd) cd.textContent = seconds + "s";
                    }
                  }, 1000);
                })();
              </script>
            </body>
            </html>
            """;
    }
}
