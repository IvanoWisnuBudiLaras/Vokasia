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
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Identity;
using Vokasia.Infrastructure.Persistence;
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
    private readonly VokasiaDbContext _db;

    public AuthorizationController(
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        VokasiaClaimsFactory claimsFactory,
        MagicLinkService magicLinkService,
        VokasiaDbContext db)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _claimsFactory = claimsFactory;
        _magicLinkService = magicLinkService;
        _db = db;
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
    // VOK-H3-E3 §3 [DEVIASI dicatat, bukan diam-diam - DECISIONS.md D25]: SENGAJA TIDAK diberi
    // [EnableRateLimiting] di sini. Dicoba dulu ("public" 10/mnt per IP), TERBUKTI SALAH lewat test
    // nyata: endpoint ini dipakai bersama oleh BANYAK user berbeda dari IP yang sama (setiap login
    // sukses + setiap refresh rutin BFF - dlm test, 1 factory/IP dipakai belasan user berbeda dlm
    // 1 class; di produksi nyata, banyak siswa di jaringan sekolah yang sama) - rate-limit IP-only
    // di sini akan mengunci user LAIN yang sah, bukan cuma penyerang (dibuktikan: JournalStudentEndpointsTests
    // - 12 login berbeda dari 1 factory - gagal 429 palsu begitu policy dipasang di sini). Kredensial
    // yang ditukar endpoint ini (authorization_code, refresh_token, magic-link token - lihat Exchange()
    // di bawah) SEMUANYA ber-entropi tinggi (sudah diterbitkan lebih dulu lewat jalur lain), TIDAK
    // bisa ditebak lewat volume percobaan spt password pendek - maka tidak butuh rate-limit ketat di
    // titik INI. Permukaan password SUNGGUHAN (POST /account/login) yang dibatasi ketat (policy
    // "login", partisi IP+email - lihat AccountEndpoints.cs + doc-comment VokasiaRateLimiting.cs).
    public async Task<IActionResult> Exchange()
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("Request OpenIddict tidak ditemukan.");

        if (request.IsAuthorizationCodeGrantType())
        {
            var result = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            var principal = result.Principal!;
            principal.SetRefreshTokenLifetime(AppTimeZone.CalculateDevRefreshTokenLifetime(DateTimeOffset.UtcNow));
            return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
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
            var principal = new ClaimsPrincipal(identity);
            principal.SetRefreshTokenLifetime(AppTimeZone.CalculateDevRefreshTokenLifetime(DateTimeOffset.UtcNow));
            return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
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
            var mentorPrincipal = new ClaimsPrincipal(mentorIdentity);
            mentorPrincipal.SetRefreshTokenLifetime(AppTimeZone.CalculateDevRefreshTokenLifetime(DateTimeOffset.UtcNow));
            return SignIn(mentorPrincipal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        if (request.GrantType == OpenIddictSetup.ImpersonationGrantType)
        {
            return await ExchangeImpersonationAsync(request);
        }

        throw new NotImplementedException("Grant type belum didukung.");
    }

    /// <summary>
    /// VOK-H6-E3 §1 StartImpersonation. Pemanggil harus SUDAH terautentikasi sbg SuperAdmin lewat
    /// Bearer access token MILIKNYA SENDIRI (HttpContext.User diisi UseAuthentication() global —
    /// lihat doc-comment ImpersonationGrantType) — BUKAN dari body request (mencegah siapa pun
    /// mengaku SuperAdmin lewat parameter, pola sama AuditEndpoints.WriteAudit). AC ticket literal:
    /// tak boleh impersonasi SuperAdmin lain (hindari rantai impersonasi tanpa akhir yang membingungkan
    /// audit trail) & user target harus aktif.
    /// </summary>
    private async Task<IActionResult> ExchangeImpersonationAsync(OpenIddictRequest request)
    {
        static IActionResult Invalid(string description) => new ForbidResult(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            new AuthenticationProperties(new Dictionary<string, string?>
            {
                [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description,
            }));

        if (HttpContext.User.Identity?.IsAuthenticated != true || HttpContext.User.FindFirst("role")?.Value != nameof(UserRole.SuperAdmin))
        {
            return Invalid("Hanya SuperAdmin (dgn access token valid miliknya sendiri) yang boleh memulai impersonasi.");
        }

        var actorSub = HttpContext.User.FindFirst(Claims.Subject)?.Value;
        if (!Guid.TryParse(actorSub, out var actorId))
        {
            return Invalid("Access token SuperAdmin tidak valid.");
        }

        var targetIdRaw = request.GetParameter("target_user_id")?.ToString();
        if (!Guid.TryParse(targetIdRaw, out var targetId))
        {
            return Invalid("Parameter target_user_id wajib diisi (GUID valid).");
        }

        var targetUser = await _userManager.FindByIdAsync(targetId.ToString());
        if (targetUser is null || !targetUser.IsActive)
        {
            return Invalid("User target tidak ditemukan atau tidak aktif.");
        }

        if (targetUser.Role == UserRole.SuperAdmin)
        {
            return Invalid("Tidak boleh mengimpersonasi sesama SuperAdmin.");
        }

        _db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = targetUser.TenantId,
            ActorUserId = actorId,
            ActingAsUserId = targetId, // diisi LANGSUNG di sini (bukan lewat auto-koreksi SaveChanges) —
            // request INI belum membawa claim impersonator_id sama sekali (token barunya BELUM dipakai),
            // jadi ITenantContext.ImpersonatorUserId masih null utk request ini; guard "is null" di
            // VokasiaDbContext.SaveChangesAsync memastikan baris ini TIDAK ikut dikoreksi ulang.
            Action = "ImpersonationStarted",
            Entity = nameof(AppUser),
            EntityId = targetId.ToString(),
            MetaJson = System.Text.Json.JsonSerializer.Serialize(new { TargetName = targetUser.FullName, TargetRole = targetUser.Role.ToString() }),
        });
        await _db.SaveChangesAsync();

        var identity = await _claimsFactory.GenerateClaimsAsync(targetUser);
        identity.AddClaim(new Claim("impersonator_id", actorId.ToString()));
        identity.SetDestinations(GetDestinations);
        var principal = new ClaimsPrincipal(identity);
        principal.SetRefreshTokenLifetime(AppTimeZone.CalculateDevRefreshTokenLifetime(DateTimeOffset.UtcNow));
        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
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
