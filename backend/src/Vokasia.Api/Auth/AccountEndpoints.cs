using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Vokasia.Api.Middleware;
using Vokasia.Api.RateLimiting;
using Vokasia.Infrastructure.Identity;

namespace Vokasia.Api.Auth;

/// <summary>
/// Provides the interactive cookie sign-in surface used when the authorization endpoint challenges
/// an unauthenticated browser. The standalone form is intentionally rendered by the backend because
/// the OAuth flow enters this origin directly. Return targets are restricted to local root-relative
/// paths; the stylesheet and small password-visibility controller are authorized by the
/// per-request nonce from
/// <see cref="SecurityHeadersMiddleware"/>.
/// </summary>
public static class AccountEndpoints
{
    private const string SafeFallbackReturnUrl = "/account/continue";

    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/account/login", GetLoginForm);
        app.MapGet("/account/continue", ContinueToFrontend);
        app.MapGet("/account/logout", GetLogout).DisableAntiforgery();
        app.MapPost("/account/logout", GetLogout).DisableAntiforgery();
        // VOK-H3-E3 §3: policy "login" (5/mnt, partisi IP+email) - INI permukaan password sungguhan
        // (lihat doc-comment VokasiaRateLimiting utk kenapa bukan /connect/token).
        app.MapPost("/account/login", PostLogin)
            .RequireRateLimiting(VokasiaRateLimiting.LoginPolicy);
        return app;
    }

    private static IResult GetLoginForm(
        HttpContext context,
        IAntiforgery antiforgery,
        [FromQuery] string? returnUrl,
        [FromQuery] string? error)
    {
        var rawReturnUrl = GetSafeReturnUrl(returnUrl);
        if (context.User.Identity?.IsAuthenticated == true)
        {
            if (rawReturnUrl == SafeFallbackReturnUrl)
            {
                var config = context.RequestServices.GetRequiredService<IConfiguration>();
                return ContinueToFrontend(config);
            }
            return SeeOther(rawReturnUrl);
        }
        var safeReturnUrl = System.Net.WebUtility.HtmlEncode(rawReturnUrl);
        var cspNonce = System.Net.WebUtility.HtmlEncode(
            SecurityHeadersMiddleware.GetCspNonce(context));
        var hasError = !string.IsNullOrWhiteSpace(error);
        var errorHtml = !hasError
            ? ""
            : $"""
                <p id="login-error" class="error" role="alert">
                  <strong>Masuk belum berhasil.</strong>
                  <span>{System.Net.WebUtility.HtmlEncode(error)}</span>
                </p>
                """;
        var formErrorReference = hasError
            ? " aria-describedby=\"login-error login-help\""
            : " aria-describedby=\"login-help\"";
        var fieldErrorAttributes = hasError
            ? " aria-invalid=\"true\" aria-describedby=\"login-error\""
            : "";
        var antiforgeryTokens = antiforgery.GetAndStoreTokens(context);
        var antiforgeryFieldName = System.Net.WebUtility.HtmlEncode(
            antiforgeryTokens.FormFieldName);
        var antiforgeryRequestToken = System.Net.WebUtility.HtmlEncode(
            antiforgeryTokens.RequestToken ??
            throw new InvalidOperationException("Token antiforgery login tidak tersedia."));

        var html = $$"""
            <!doctype html>
            <html lang="id">
            <head>
              <meta charset="utf-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover" />
              <meta name="color-scheme" content="light" />
              <title>Masuk — Vokasia</title>
              <style nonce="{{cspNonce}}">
                /* Hallmark · pre-emit critique: P5 H4 E5 S5 R5 V4
                 * Hallmark · genre: modern-minimal · tone: utilitarian · palette: sekolah 213°
                 * Hallmark · macrostructure: focused-sign-in · design-system: DESIGN.md
                 */
                :root {
                  --color-surface: oklch(99% 0.009 94);
                  --color-surface-muted: oklch(96% 0.012 94);
                  --color-ink: oklch(18% 0.03 213);
                  --color-ink-muted: oklch(48% 0.03 213);
                  --color-border: oklch(65% 0.06 213);
                  --color-primary: oklch(50% 0.14 213);
                  --color-primary-hover: oklch(45% 0.14 213);
                  --color-primary-ink: oklch(99% 0.006 213);
                  --color-primary-muted: oklch(91.5% 0.074 210.8);
                  --color-focus: oklch(55% 0.18 213);
                  --color-accent-bright: oklch(81.6% 0.121 212.8);
                  --color-status-red: oklch(50% 0.16 25);
                  --color-status-red-bg: oklch(95% 0.03 25);
                  --font-display: "Geist", "Segoe UI", sans-serif;
                  --font-body: "Geist", "Segoe UI", sans-serif;
                  --space-1: 4px;
                  --space-2: 8px;
                  --space-3: 12px;
                  --space-4: 16px;
                  --space-5: 20px;
                  --space-6: 24px;
                  --space-8: 32px;
                  --radius-sm: 6px;
                  --radius-md: 10px;
                  --radius-lg: 16px;
                  --tap-min: 44px;
                  --dur-fast: 120ms;
                  --ease-out: cubic-bezier(0.16, 1, 0.3, 1);
                  --shadow-whisper: 0 1px 2px oklch(18% 0.03 213 / 0.08);
                }

                * {
                  box-sizing: border-box;
                }

                html,
                body {
                  min-block-size: 100%;
                  overflow-x: clip;
                }

                body {
                  margin: 0;
                  background: var(--color-surface-muted);
                  color: var(--color-ink);
                  font-family: var(--font-body);
                  font-size: 1rem;
                  line-height: 1.5;
                }

                button,
                input {
                  font: inherit;
                }

                .page {
                  display: grid;
                  min-block-size: 100svh;
                  align-items: center;
                  padding-block:
                    max(var(--space-8), env(safe-area-inset-top))
                    max(var(--space-8), env(safe-area-inset-bottom));
                  padding-inline:
                    max(var(--space-4), env(safe-area-inset-left))
                    max(var(--space-4), env(safe-area-inset-right));
                }

                .login-shell {
                  inline-size: min(100%, 420px);
                  margin-inline: auto;
                }

                .login-card {
                  border: 1px solid var(--color-border);
                  border-radius: var(--radius-lg);
                  background: var(--color-surface);
                  padding: var(--space-6);
                  box-shadow: var(--shadow-whisper);
                }

                .brand {
                  display: flex;
                  align-items: center;
                  gap: var(--space-3);
                }

                .brand-mark {
                  display: grid;
                  flex: 0 0 var(--tap-min);
                  inline-size: var(--tap-min);
                  min-block-size: var(--tap-min);
                  place-items: center;
                  border: 1px solid var(--color-border);
                  border-radius: var(--radius-md);
                  background: var(--color-primary-muted);
                  color: var(--color-primary);
                  font-family: var(--font-display);
                  font-weight: 800;
                }

                .brand-copy {
                  min-inline-size: 0;
                }

                .brand-name,
                .brand-caption,
                .lede,
                .privacy {
                  margin: 0;
                }

                .brand-name {
                  color: var(--color-primary);
                  font-size: 0.75rem;
                  font-weight: 700;
                  letter-spacing: 0.1em;
                }

                .brand-caption {
                  color: var(--color-ink-muted);
                  font-size: 0.875rem;
                }

                .intro {
                  display: grid;
                  gap: var(--space-2);
                  margin-block-start: var(--space-6);
                }

                h1 {
                  min-inline-size: 0;
                  margin: 0;
                  overflow-wrap: anywhere;
                  color: var(--color-ink);
                  font-family: var(--font-display);
                  font-size: clamp(1.75rem, 7vw, 2.25rem);
                  font-style: normal;
                  font-weight: 700;
                  letter-spacing: -0.025em;
                  line-height: 1.15;
                }

                .lede {
                  max-inline-size: 45ch;
                  color: var(--color-ink-muted);
                }

                .error {
                  display: grid;
                  gap: var(--space-1);
                  margin: var(--space-5) 0 0;
                  border: 1px solid var(--color-status-red);
                  border-radius: var(--radius-md);
                  background: var(--color-status-red-bg);
                  color: var(--color-status-red);
                  padding: var(--space-3);
                }

                .form {
                  display: grid;
                  gap: var(--space-5);
                  margin-block-start: var(--space-6);
                }

                .field {
                  display: grid;
                  gap: var(--space-2);
                }

                label {
                  color: var(--color-ink);
                  font-weight: 700;
                }

                .control,
                .submit,
                .password-toggle {
                  min-block-size: var(--tap-min);
                  border-radius: var(--radius-md);
                  outline: 2px solid transparent;
                }

                .control,
                .submit {
                  inline-size: 100%;
                }

                .control {
                  border: 1px solid var(--color-border);
                  outline-offset: 1px;
                  background: var(--color-surface);
                  color: var(--color-ink);
                  padding: var(--space-2) var(--space-3);
                }

                .password-row {
                  display: grid;
                  grid-template-columns: minmax(0, 1fr) auto;
                  align-items: stretch;
                  gap: var(--space-2);
                }

                .password-toggle {
                  border: 1px solid var(--color-border);
                  outline-offset: 1px;
                  background: var(--color-surface-muted);
                  color: var(--color-primary);
                  cursor: pointer;
                  font-weight: 700;
                  padding: var(--space-2) var(--space-3);
                  white-space: nowrap;
                  transition: transform var(--dur-fast) var(--ease-out);
                }

                .password-toggle[hidden] {
                  display: none;
                }

                .password-status {
                  position: absolute;
                  inline-size: 1px;
                  block-size: 1px;
                  overflow: hidden;
                  clip-path: inset(50%);
                  white-space: nowrap;
                }

                .control:focus-visible,
                .submit:focus-visible,
                .password-toggle:focus-visible {
                  outline-color: var(--color-focus);
                }

                .control:disabled,
                .submit:disabled,
                .password-toggle:disabled {
                  cursor: not-allowed;
                  opacity: 0.55;
                }

                .submit {
                  border: 1px solid var(--color-primary);
                  outline-offset: 2px;
                  background: var(--color-primary);
                  color: var(--color-primary-ink);
                  cursor: pointer;
                  font-weight: 700;
                  padding: var(--space-2) var(--space-4);
                  white-space: nowrap;
                  transition: transform var(--dur-fast) var(--ease-out);
                }

                .submit:active {
                  transform: translateY(1px);
                }

                .control:active {
                  border-color: var(--color-primary);
                }

                .password-toggle:active {
                  border-color: var(--color-primary);
                  background: var(--color-primary-muted);
                  color: var(--color-primary-hover);
                  transform: translateY(1px);
                }

                .privacy {
                  margin-block-start: var(--space-4);
                  color: var(--color-ink-muted);
                  font-size: 0.875rem;
                  line-height: 1.5;
                }

                @media (hover: hover) and (pointer: fine) {
                  .control:hover {
                    background: var(--color-surface-muted);
                  }

                  .submit:hover {
                    background: var(--color-primary-hover);
                  }

                  .password-toggle:hover {
                    border-color: var(--color-primary);
                    background: var(--color-primary-muted);
                    color: var(--color-primary-hover);
                  }
                }

                @media (max-width: 22rem) {
                  .password-row {
                    grid-template-columns: minmax(0, 1fr);
                  }

                  .password-toggle {
                    inline-size: 100%;
                  }
                }

                @media (min-width: 40rem) {
                  .login-card {
                    padding: var(--space-8);
                  }
                }

                @media (prefers-reduced-motion: reduce) {
                  *,
                  *::before,
                  *::after {
                    animation-duration: 0.01ms !important;
                    animation-iteration-count: 1 !important;
                    transition-duration: 0.01ms !important;
                  }
                }
              </style>
            </head>
            <body data-theme="sekolah">
              <main class="page">
                <section class="login-shell" aria-labelledby="login-title">
                  <div class="login-card">
                    <header class="brand">
                      <span class="brand-mark" aria-hidden="true">V</span>
                      <div class="brand-copy">
                        <p class="brand-name">VOKASIA · PKL SMK</p>
                        <p class="brand-caption">Ruang belajar dan bimbingan</p>
                      </div>
                    </header>

                    <div class="intro">
                      <h1 id="login-title">Masuk ke ruang PKL-mu</h1>
                      <p class="lede">
                        Gunakan akun siswa, mentor, atau staf yang diberikan sekolah maupun pengelola Vokasia.
                      </p>
                    </div>

                    {{errorHtml}}

                    <form class="form" method="post" action="/account/login"{{formErrorReference}}>
                      <input type="hidden" name="returnUrl" value="{{safeReturnUrl}}" />
                      <input
                        type="hidden"
                        name="{{antiforgeryFieldName}}"
                        value="{{antiforgeryRequestToken}}"
                      />

                      <div class="field">
                        <label for="email">Email</label>
                        <input{{fieldErrorAttributes}}
                          class="control"
                          id="email"
                          name="email"
                          type="email"
                          inputmode="email"
                          autocomplete="username"
                          autocapitalize="none"
                          spellcheck="false"
                          aria-required="true"
                          required
                        />
                      </div>

                      <div class="field">
                        <label for="password">Kata sandi</label>
                        <div class="password-row">
                          <input{{fieldErrorAttributes}}
                            class="control"
                            id="password"
                            name="password"
                            type="password"
                            autocomplete="current-password"
                            autocapitalize="none"
                            spellcheck="false"
                            aria-required="true"
                            required
                          />
                          <button
                            class="password-toggle"
                            id="password-toggle"
                            type="button"
                            aria-controls="password"
                            aria-label="Tampilkan kata sandi"
                            hidden
                          >Tampilkan</button>
                        </div>
                        <span
                          class="password-status"
                          id="password-status"
                          aria-live="polite"
                        ></span>
                      </div>

                      <button class="submit" type="submit">Masuk</button>
                    </form>

                    <p class="privacy" id="login-help">
                      Kata sandi dipakai hanya untuk memeriksa akunmu saat masuk.
                    </p>
                  </div>
                </section>
              </main>
              <script nonce="{{cspNonce}}">
                (() => {
                  const password = document.getElementById("password");
                  const toggle = document.getElementById("password-toggle");
                  const status = document.getElementById("password-status");
                  if (!(password instanceof HTMLInputElement) ||
                      !(toggle instanceof HTMLButtonElement) ||
                      !(status instanceof HTMLElement)) {
                    return;
                  }

                  toggle.addEventListener("click", () => {
                    const reveal = password.type === "password";
                    password.type = reveal ? "text" : "password";
                    toggle.setAttribute(
                      "aria-label",
                      reveal ? "Sembunyikan kata sandi" : "Tampilkan kata sandi");
                    toggle.textContent = reveal ? "Sembunyikan" : "Tampilkan";
                    status.textContent = reveal
                      ? "Kata sandi ditampilkan."
                      : "Kata sandi disembunyikan.";
                  });
                  toggle.hidden = false;
                })();
              </script>
            </body>
            </html>
            """;

        return Results.Content(html, "text/html");
    }

    private static async Task<IResult> PostLogin(
        HttpRequest req,
        IAntiforgery antiforgery,
        UserManager<AppUser> userManager,
        CancellationToken ct)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(req.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            var attemptedForm = req.HasFormContentType
                ? await req.ReadFormAsync(ct)
                : null;
            var safeAttemptedReturnUrl = GetSafeReturnUrl(
                attemptedForm?["returnUrl"].ToString());
            var reason =
                "Form masuk sudah kedaluwarsa atau tidak valid. Muat ulang lalu coba lagi.";
            return SeeOther(
                $"/account/login?returnUrl={Uri.EscapeDataString(safeAttemptedReturnUrl)}" +
                $"&error={Uri.EscapeDataString(reason)}");
        }

        var form = await req.ReadFormAsync(ct);
        var email = form["email"].ToString();
        var password = form["password"].ToString();
        var returnUrl = GetSafeReturnUrl(System.Net.WebUtility.HtmlDecode(form["returnUrl"].ToString()));

        var user = await userManager.FindByEmailAsync(email);
        var passwordOk = user is not null && await userManager.CheckPasswordAsync(user, password);
        Console.WriteLine($"[POST LOGIN DEBUG] Email={email}, UserFound={user != null}, PasswordOk={passwordOk}, IsActive={user?.IsActive}");

        if (user is null || !user.IsActive || !passwordOk)
        {
            const string reason =
                "Email atau kata sandi salah. Periksa kembali lalu coba masuk.";
            var redirectUrl = $"/account/login?returnUrl={Uri.EscapeDataString(returnUrl)}&error={Uri.EscapeDataString(reason)}";
            Console.WriteLine($"[POST LOGIN DEBUG] FAILED. Redirecting to {redirectUrl}");
            return SeeOther(redirectUrl);
        }

        // mendaftar 1 scheme cookie bernama "Cookies") — pakai SignInManager di sini akan
        // gagal runtime ("no authentication handler registered for Identity.Application").
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.UserName ?? email)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        await req.HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));


        return SeeOther(returnUrl);
    }

    // Keep this predicate aligned with frontend/src/lib/localReturnUrl.ts:getSafeLocalReturnUrl.
    // Only the invalid-value fallback intentionally differs: /account/continue here, null there.
    private static string GetSafeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl) ||
            returnUrl[0] != '/' ||
            (returnUrl.Length > 1 && returnUrl[1] == '/') ||
            returnUrl.Contains('\\') ||
            returnUrl.Any(char.IsControl))
        {
            return SafeFallbackReturnUrl;
        }

        return returnUrl;
    }

    private static IResult ContinueToFrontend(IConfiguration configuration)
    {
        var configuredUrl =
            configuration["Frontend:PublicUrl"] ??
            configuration["NEXT_PUBLIC_APP_URL"] ??
            "http://localhost:3000";
        if (!Uri.TryCreate(configuredUrl, UriKind.Absolute, out var frontendUrl) ||
            (frontendUrl.Scheme != Uri.UriSchemeHttp &&
             frontendUrl.Scheme != Uri.UriSchemeHttps))
        {
            frontendUrl = new Uri("http://localhost:3000");
        }

        var targetUrl = new Uri(frontendUrl, "/api/auth/login");
        return SeeOther(targetUrl.ToString());
    }

    private static async Task<IResult> GetLogout(
        HttpContext context,
        IConfiguration configuration)
    {
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        var cookieOpts = new CookieOptions { Path = "/", HttpOnly = true, SameSite = SameSiteMode.Lax };
        context.Response.Cookies.Delete(CookieAuthenticationDefaults.AuthenticationScheme, cookieOpts);
        context.Response.Cookies.Delete(".AspNetCore.Cookies", cookieOpts);
        context.Response.Cookies.Delete(".AspNetCore.Identity.Application", cookieOpts);
        context.Response.Cookies.Delete("Cookies", cookieOpts);

        var configuredUrl =
            configuration["Frontend:PublicUrl"] ??
            configuration["NEXT_PUBLIC_APP_URL"] ??
            "http://localhost:3000";
        if (!Uri.TryCreate(configuredUrl, UriKind.Absolute, out var frontendUrl) ||
            (frontendUrl.Scheme != Uri.UriSchemeHttp &&
             frontendUrl.Scheme != Uri.UriSchemeHttps))
        {
            frontendUrl = new Uri("http://localhost:3000");
        }

        var targetUrl = new Uri(frontendUrl, "/login");
        return SeeOther(targetUrl.ToString());
    }

    /// <summary>
    /// GAP ditemukan+ditambal sesi VOK-H2-E3 (DECISIONS.md D17): <c>Results.Redirect()</c> default
    /// (302 Found) meninggalkan METODE lanjutan ambigu antar client — ketahuan lewat smoke test
    /// HTTP asli: curl mengirim ulang sbg POST TANPA Content-Type/body ke <c>/connect/authorize</c>
    /// (yang JUGA menerima POST — lihat AuthorizationController), ditolak OpenIddict ("mandatory
    /// Content-Type header missing"). 303 See Other = pola Post/Redirect/Get baku, TIDAK ambigu
    /// di client mana pun (wajib GET ke Location, titik) — pas persis utk "form login selesai,
    /// lanjut ke halaman berikutnya".
    /// </summary>
    private static IResult SeeOther(string location) => new SeeOtherResult(location);

    private sealed class SeeOtherResult(string location) : IResult
    {
        public Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.StatusCode = StatusCodes.Status303SeeOther;
            httpContext.Response.Headers.Location = location;
            return Task.CompletedTask;
        }
    }
}
