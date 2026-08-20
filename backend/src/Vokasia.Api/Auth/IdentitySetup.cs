using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Validation.AspNetCore;
using Vokasia.Infrastructure;
using Vokasia.Infrastructure.Identity;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Api.Auth;

/// <summary>
/// Satu-satunya titik registrasi ASP.NET Identity (FR-AUTH-01, AGENTS.md §3). Sengaja terpisah
/// dari Vokasia.Infrastructure/DependencyInjection.cs (yang hanya DbContext/Redis/MinIO) agar
/// tidak ada dua panggilan AddIdentityCore yang saling menimpa opsi (DECISIONS.md D14).
/// </summary>
public static class IdentitySetup
{
    public static IServiceCollection AddVokasiaIdentity(this IServiceCollection services)
    {
        services.AddVokasiaIdentityCore()
            .AddSignInManager<SignInManager<AppUser>>();
        services.AddScoped<VokasiaClaimsFactory>();

        // GAP ditemukan+ditambal sesi VOK-H2-E3 (DECISIONS.md D17): default scheme di sini SEBELUMNYA
        // Cookie ("Cookies") — bikin SEMUA endpoint resource (RequireAuthorization(RbacPolicies.*),
        // tanpa AddAuthenticationSchemes eksplisit) diam-diam diautentikasi lewat handler Cookie
        // SAJA, apa pun isi header Authorization: Bearer. Ketahuan lewat smoke test HTTP nyata:
        // GET /api/students dgn Bearer token JWT yang VALID (baru diterbitkan, belum kedaluwarsa)
        // tetap balas 302 ke /account/login (perilaku "challenge" khas Cookie handler thd request
        // tanpa cookie valid) — bukan 401 khas Bearer/JWT gagal validasi, bukan pula 200. Handler
        // OpenIddict.Validation ("OpenIddict.Validation.AspNetCore", didaftarkan
        // OpenIddictSetup.cs .AddValidation()) SUDAH terdaftar sejak H1-E3 tapi TIDAK PERNAH
        // benar2 dipanggil utk endpoint resource krn bukan default scheme — lolos tanpa terdeteksi
        // di H1-E3/H2-E1 krn test yang ada set ClaimsPrincipal cookie langsung (in-process), tidak
        // pernah lewat header Authorization: Bearer sungguhan. Default scheme sekarang OpenIddict
        // Validation (jalur UTAMA: endpoint resource dipanggil BFF via Bearer) — Cookie TETAP
        // terdaftar+dipakai, hanya SELALU via nama skema eksplisit (AuthorizationController.
        // Authorize() & AccountEndpoints sudah begitu dari awal, jadi TIDAK terdampak perubahan ini).
        services.AddAuthentication(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)
            .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
            {
                options.LoginPath = "/account/login"; // form login sederhana H1; UI penuh H2-E2.
                options.LogoutPath = "/account/logout";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.Path = "/";
            });

        return services;
    }
}
