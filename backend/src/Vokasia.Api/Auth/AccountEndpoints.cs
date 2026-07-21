using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Vokasia.Api.RateLimiting;
using Vokasia.Infrastructure.Identity;

namespace Vokasia.Api.Auth;

/// <summary>
/// GAP ditemukan+ditambal sesi VOK-H2-E3 (DECISIONS.md D17): <c>IdentitySetup.cs</c> menetapkan
/// cookie scheme <c>LoginPath = "/account/login"</c> sejak H1-E3, dan <c>AuthorizationController
/// .Authorize()</c> memanggil <c>Challenge()</c> yang mengarah ke path itu — tapi endpoint-nya
/// sendiri belum pernah dibuat, jadi flow interaktif nyata (`/connect/authorize` tanpa cookie)
/// selama ini mengarah ke 404. Tidak ketahuan di H1-E3 karena test yang ada (Vokasia.Tests/Auth/)
/// menguji claims/lifetime/PKCE-rejection lewat cookie yang SUDAH diset manual di test, bukan
/// lewat form login sungguhan. Form di sini SANGAT sederhana (tulisan ticket H1-E3 sendiri:
/// "hanya untuk membuktikan flow code+PKCE hidup") — UI produksi ada di Next.js (H2-E2), yang
/// hanya redirect ke BFF; BFF lalu redirect ke sini HANYA jika belum ada cookie.
/// </summary>
public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/account/login", GetLoginForm);
        // VOK-H3-E3 §3: policy "login" (5/mnt, partisi IP+email) - INI permukaan password sungguhan
        // (lihat doc-comment VokasiaRateLimiting utk kenapa bukan /connect/token).
        app.MapPost("/account/login", PostLogin).DisableAntiforgery().RequireRateLimiting(VokasiaRateLimiting.LoginPolicy);
        return app;
    }

    private static IResult GetLoginForm([FromQuery] string? returnUrl, [FromQuery] string? error)
    {
        var safeReturnUrl = System.Net.WebUtility.HtmlEncode(returnUrl ?? "/");
        var errorHtml = string.IsNullOrEmpty(error)
            ? ""
            : $"<p style=\"color:#c0392b\">{System.Net.WebUtility.HtmlEncode(error)}</p>";

        var html = $$"""
            <!doctype html>
            <html lang="id">
            <head><meta charset="utf-8" /><title>Masuk - Vokasia (dev)</title></head>
            <body style="font-family:system-ui,sans-serif;max-width:360px;margin:80px auto">
              <h1 style="font-size:1.25rem">Masuk (form dev H1-E3)</h1>
              <p style="color:#666;font-size:.85em">Form sangat sederhana untuk membuktikan flow code+PKCE hidup. UI produksi ada di Next.js.</p>
              {{errorHtml}}
              <form method="post" action="/account/login">
                <input type="hidden" name="returnUrl" value="{{safeReturnUrl}}" />
                <p><input name="email" type="email" placeholder="email" required style="width:100%;padding:8px;box-sizing:border-box" /></p>
                <p><input name="password" type="password" placeholder="password" required style="width:100%;padding:8px;box-sizing:border-box" /></p>
                <button type="submit" style="padding:8px 16px">Masuk</button>
              </form>
            </body>
            </html>
            """;

        return Results.Content(html, "text/html");
    }

    private static async Task<IResult> PostLogin(HttpRequest req, UserManager<AppUser> userManager, CancellationToken ct)
    {
        var form = await req.ReadFormAsync(ct);
        var email = form["email"].ToString();
        var password = form["password"].ToString();
        var returnUrl = form["returnUrl"].ToString();
        if (string.IsNullOrEmpty(returnUrl))
        {
            returnUrl = "/";
        }

        var user = await userManager.FindByEmailAsync(email);
        var passwordOk = user is not null && await userManager.CheckPasswordAsync(user, password);

        if (user is null || !user.IsActive || !passwordOk)
        {
            var reason = user is not null && !user.IsActive ? "Akun nonaktif." : "Email atau password salah.";
            var redirectUrl = $"/account/login?returnUrl={Uri.EscapeDataString(returnUrl)}&error={Uri.EscapeDataString(reason)}";
            return SeeOther(redirectUrl);
        }

        // Sign-in eksplisit di scheme "Cookies" (CookieAuthenticationDefaults.AuthenticationScheme)
        // — SAMA PERSIS dgn yang dibaca AuthorizationController.Authorize() via
        // HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme).
        // TIDAK pakai SignInManager.PasswordSignInAsync: scheme default SignInManager
        // (IdentityConstants.ApplicationScheme = "Identity.Application") TIDAK terdaftar di
        // IdentitySetup.cs (proyek ini pakai AddIdentityCore, bukan AddIdentity, dan cuma
        // mendaftar 1 scheme cookie bernama "Cookies") — pakai SignInManager di sini akan
        // gagal runtime ("no authentication handler registered for Identity.Application").
        var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())); // dibaca UserManager.GetUserAsync
        identity.AddClaim(new Claim(ClaimTypes.Name, user.UserName ?? email));

        await req.HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

        return SeeOther(returnUrl);
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
