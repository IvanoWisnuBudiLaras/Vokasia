using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Vokasia.Api.Middleware;
using Vokasia.Api.RateLimiting;
using Vokasia.Infrastructure.Configuration;
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
    private const string VokasiaMarkSvg = """
        <svg width="36" height="36" viewBox="0 0 192 192" fill="none" xmlns="http://www.w3.org/2000/svg" role="img" aria-labelledby="title">
          <title id="title">Vokasia</title>
          <rect width="192" height="192" rx="40" fill="#fffdf6"/>
          <path d="M37 52c0-8 6-14 14-14h90c8 0 14 6 14 14v84c0 8-6 14-14 14H51c-8 0-14-6-14-14V52Z" fill="#197b9c"/>
          <path d="M58 65h76v15H58zm0 26h49v15H58zm0 26h76v15H58z" fill="#a8f1ff"/>
          <circle cx="132" cy="98" r="18" fill="#fffdf6"/>
          <path d="m124 98 6 6 11-13" fill="none" stroke="#197b9c" stroke-linecap="round" stroke-linejoin="round" stroke-width="6"/>
        </svg>
        """;

    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/favicon.svg", () => Results.Content(VokasiaMarkSvg, "image/svg+xml"));
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

    private static async Task<IResult> GetLoginForm(
        HttpContext context,
        IAntiforgery antiforgery,
        [FromQuery] string? returnUrl,
        [FromQuery] string? error)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }

        var rawReturnUrl = GetSafeReturnUrl(returnUrl);
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
        var formErrorReference = "";
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
              <link rel="icon" href="/favicon.svg" type="image/svg+xml" />
              <title>Masuk — Vokasia</title>
              <style nonce="{{cspNonce}}">
                /* Hallmark · pre-emit critique: P5 H4 E5 S5 R5 V4
                 * Hallmark · genre: clean-coastal · tone: professional · palette: V2.1 Clean Coastal
                 * Hallmark · macrostructure: focused-sign-in · design-system: DESIGN.md D45
                 */
                :root {
                  --color-surface: oklch(100% 0.000 0);
                  --color-surface-muted: oklch(98% 0.005 250);
                  --color-ink: oklch(19% 0.003 110);
                  --color-ink-muted: oklch(51% 0.03 250);
                  --color-border: oklch(90% 0.01 250);
                  --color-primary: oklch(50.4% 0.162 243.3);
                  --color-primary-hover: oklch(45% 0.15 243.3);
                  --color-primary-ink: oklch(100% 0.000 0);
                  --color-primary-muted: oklch(96.3% 0.021 243.3);
                  --color-focus: oklch(60.1% 0.165 243.3);
                  --color-accent-bright: oklch(60.1% 0.165 243.3);
                  --color-status-red: oklch(55% 0.20 25);
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
                  --radius-sm: 8px;
                  --radius-md: 12px;
                  --radius-lg: 16px;
                  --tap-min: 44px;
                  --dur-fast: 120ms;
                  --ease-out: cubic-bezier(0.16, 1, 0.3, 1);
                  --shadow-whisper: 0 8px 30px rgb(2,132,199,0.06);
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
                  background: var(--color-surface);
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
                  inline-size: min(100%, 480px);
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
                  justify-content: center;
                  margin-block-end: var(--space-6);
                }

                .brand-badge {
                  display: inline-flex;
                  align-items: center;
                  gap: var(--space-3);
                }

                .brand-title {
                  color: var(--color-ink);
                  font-family: var(--font-display);
                  font-size: 1.35rem;
                  font-weight: 800;
                  letter-spacing: -0.025em;
                }

                .intro {
                  display: grid;
                  gap: var(--space-2);
                  text-align: center;
                  margin-block-end: var(--space-6);
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
                .submit {
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
                  transition: all var(--dur-fast) var(--ease-out);
                }

                .control:focus-visible, .control:focus {
                  background: var(--color-surface);
                  border-color: var(--color-primary);
                  outline: 2px solid var(--color-primary);
                }

                .password-checkbox-row {
                  display: inline-flex;
                  align-items: center;
                  gap: var(--space-2);
                  margin-block-start: var(--space-2);
                  cursor: pointer;
                  color: var(--color-ink-muted);
                  font-size: 0.875rem;
                  font-weight: 500;
                  user-select: none;
                }

                .password-checkbox {
                  inline-size: 18px;
                  block-size: 18px;
                  margin: 0;
                  border: 1px solid var(--color-border);
                  border-radius: 4px;
                  accent-color: var(--color-primary);
                  cursor: pointer;
                  outline-offset: 2px;
                }

                .password-checkbox:focus-visible {
                  outline: 2px solid var(--color-focus);
                }

                .password-checkbox-row:hover span {
                  color: var(--color-ink);
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
                .submit:focus-visible {
                  outline-color: var(--color-focus);
                }

                .control:disabled,
                .submit:disabled {
                  cursor: not-allowed;
                  opacity: 0.55;
                }

                .submit {
                  border: none;
                  outline-offset: 2px;
                  background: var(--color-primary);
                  color: var(--color-primary-ink);
                  cursor: pointer;
                  font-weight: 700;
                  padding: var(--space-2) var(--space-4);
                  white-space: nowrap;
                  box-shadow: 0 2px 4px 0 oklch(50.4% 0.162 243.3 / 0.2);
                  transition: all var(--dur-fast) var(--ease-out);
                }

                .submit:hover {
                  background: var(--color-primary-hover);
                  transform: translateY(-1px);
                  box-shadow: 0 4px 8px 0 oklch(50.4% 0.162 243.3 / 0.25);
                }

                .submit:active {
                  transform: translateY(0);
                  box-shadow: 0 2px 4px 0 oklch(50.4% 0.162 243.3 / 0.2);
                }

                .control:active {
                  border-color: var(--color-primary);
                }

                @media (hover: hover) and (pointer: fine) {
                  .control:hover {
                    border-color: var(--color-primary);
                    background: var(--color-surface);
                  }
                }
                @media (min-width: 40rem) {
                  .login-card {
                    padding: var(--space-8);
                  }
                }
              </style>
            </head>
            <body data-theme="sekolah">
              <main class="page">
                <section class="login-shell" aria-labelledby="login-title">
                  <div class="login-card">
                    <header class="brand">
                      <div class="brand-badge">
                        {{VokasiaMarkSvg}}
                        <span class="brand-title">Vokasia</span>
                      </div>
                    </header>

                    <div class="intro">
                      <h1 id="login-title">Masuk ke Vokasia</h1>
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
                        <label class="password-checkbox-row" for="password-toggle">
                          <input type="checkbox" id="password-toggle" class="password-checkbox" />
                          <span>Tampilkan kata sandi</span>
                        </label>
                        <span
                          class="password-status"
                          id="password-status"
                          aria-live="polite"
                        ></span>
                      </div>

                      <button class="submit" type="submit">Masuk</button>
                    </form>
                  </div>
                </section>
              </main>
              <script nonce="{{cspNonce}}">
                (() => {
                  const password = document.getElementById("password");
                  const toggle = document.getElementById("password-toggle");
                  const status = document.getElementById("password-status");
                  if (!(password instanceof HTMLInputElement) ||
                      !(toggle instanceof HTMLInputElement) ||
                      !(status instanceof HTMLElement)) {
                    return;
                  }

                  toggle.addEventListener("change", () => {
                    const reveal = toggle.checked;
                    password.type = reveal ? "text" : "password";
                    status.textContent = reveal
                      ? "Kata sandi ditampilkan."
                      : "Kata sandi disembunyikan.";
                  });
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
        if (user is null || !user.IsActive || !passwordOk)
        {
            const string reason =
                "Email atau kata sandi salah. Periksa kembali lalu coba masuk.";
            var redirectUrl = $"/account/login?returnUrl={Uri.EscapeDataString(returnUrl)}&error={Uri.EscapeDataString(reason)}";
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
        var targetUrl = new Uri(new Uri(PublicAppOrigin.Resolve(configuration)), "/api/auth/login");
        return SeeOther(targetUrl.ToString());
    }

    private static async Task<IResult> GetLogout(
        HttpContext context,
        IConfiguration configuration)
    {
        context.Response.Headers.CacheControl = "no-store, max-age=0";
        context.Response.Headers["Clear-Site-Data"] = "\"cache\"";
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        var cookieOpts = new CookieOptions { Path = "/", HttpOnly = true, SameSite = SameSiteMode.Lax };
        context.Response.Cookies.Delete(CookieAuthenticationDefaults.AuthenticationScheme, cookieOpts);
        context.Response.Cookies.Delete(".AspNetCore.Cookies", cookieOpts);
        context.Response.Cookies.Delete(".AspNetCore.Identity.Application", cookieOpts);
        context.Response.Cookies.Delete("Cookies", cookieOpts);

        var targetUrl = new Uri(new Uri(PublicAppOrigin.Resolve(configuration)), "/login?logged_out=1");
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
