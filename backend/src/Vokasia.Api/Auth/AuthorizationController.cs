using Microsoft.AspNetCore; // GetOpenIddictServerRequest() ekstensi ada di sini (OpenIddict 7.x), bukan di OpenIddict.Server.AspNetCore
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using System.Security.Claims;
using Vokasia.Api.Auth.MagicLink;
using Vokasia.Infrastructure.Identity;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Vokasia.Api.Auth;

/// <summary>
/// Endpoint OAuth server (FR-AUTH-01/02). Login form H1 sangat sederhana (email+password via
/// cookie auth) — hanya untuk membuktikan flow code+PKCE hidup; UI login nyata milik H2-E2,
/// BFF exchange nyata milik H2-E3. Refresh rotation+reuse detection PENUH ditegakkan di
/// H2-E3 (BFF/Redis) — endpoint ini menerbitkan sesuai request OpenIddict standar.
/// </summary>
[ApiController]
public class AuthorizationController : ControllerBase
{
    private readonly UserManager<AppUser> _userManager;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly VokasiaClaimsFactory _claimsFactory;
    private readonly MagicLinkService _magicLinkService;

    public AuthorizationController(
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        VokasiaClaimsFactory claimsFactory,
        MagicLinkService magicLinkService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _claimsFactory = claimsFactory;
        _magicLinkService = magicLinkService;
    }

    [HttpGet("~/connect/authorize")]
    [HttpPost("~/connect/authorize")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Authorize()
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("Request OpenIddict tidak ditemukan.");

        var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (result?.Succeeded != true)
        {
            // GAP ditemukan+ditambal sesi VOK-H2-E3 (DECISIONS.md D17): Challenge() bawaan
            // menghasilkan 401 + header Location (bukan 3xx sungguhan) — browser TIDAK mengikuti
            // 401 otomatis, jadi flow interaktif nyata macet total di sini (ketahuan lewat smoke
            // test HTTP asli, bukan test tersembunyi). Redirect() manual = 302 Found biasa, pasti
            // diikuti browser (dan curl -L). Belum login -> arahkan ke halaman login sederhana
            // (H1), UI penuh di H2-E2.
            var returnUrl = Request.PathBase + Request.Path + Request.QueryString;
            return Redirect($"/account/login?ReturnUrl={Uri.EscapeDataString(returnUrl)}");
        }

        var user = await _userManager.GetUserAsync(result.Principal!)
            ?? throw new InvalidOperationException("User tidak ditemukan dari cookie session.");

        var identity = await _claimsFactory.GenerateClaimsAsync(user);
        identity.SetScopes(request.GetScopes());
        identity.SetDestinations(GetDestinations);

        return SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [HttpPost("~/connect/token")]
    [IgnoreAntiforgeryToken]
    [Produces("application/json")]
    public async Task<IActionResult> Exchange()
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("Request OpenIddict tidak ditemukan.");

        if (request.IsAuthorizationCodeGrantType())
        {
            var result = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            return SignIn(result.Principal!, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        if (request.IsRefreshTokenGrantType())
        {
            var result = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            var user = await _userManager.GetUserAsync(result.Principal!);
            if (user is null || !user.IsActive)
            {
                return Forbid(
                    authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    properties: new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "User tidak aktif atau tidak ditemukan.",
                    }));
            }

            var identity = await _claimsFactory.GenerateClaimsAsync(user);
            identity.SetDestinations(GetDestinations);
            return SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        if (request.GrantType == OpenIddictSetup.MagicLinkGrantType)
        {
            // VOK-H2-E3 §3 ExchangeMagicToken — mentor tak punya password (FR-AUTH-03), tak bisa
            // lewat authorization_code (butuh cookie login lolos AuthenticateAsync). Grant kustom
            // ini validasi+konsumsi token (MagicLinkService), lalu terbitkan identity SAMA PERSIS
            // (VokasiaClaimsFactory) spt grant lain — access/refresh token dari jalur OpenIddict
            // yang sama, bukan sesi ad-hoc paralel.
            var token = request.GetParameter("token")?.ToString();
            if (string.IsNullOrEmpty(token))
            {
                return Forbid(
                    authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    properties: new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "Parameter token wajib diisi.",
                    }));
            }

            var (ok, user, error) = await _magicLinkService.ExchangeAsync(token, HttpContext.RequestAborted);
            if (!ok || user is null)
            {
                return Forbid(
                    authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    properties: new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = error ?? "Token magic link tidak valid.",
                    }));
            }

            var mentorIdentity = await _claimsFactory.GenerateClaimsAsync(user);
            mentorIdentity.SetDestinations(GetDestinations);
            return SignIn(new ClaimsPrincipal(mentorIdentity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        throw new NotImplementedException("Grant type belum didukung.");
    }

    [HttpGet("~/connect/logout")]
    [HttpPost("~/connect/logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return SignOut(
            authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            properties: new AuthenticationProperties { RedirectUri = "/" });
    }

    private static IEnumerable<string> GetDestinations(Claim claim)
    {
        // sub, tenant_id, role, name masuk access token (dibaca RBAC/tenant filter H2-E3).
        yield return Destinations.AccessToken;
    }
}
