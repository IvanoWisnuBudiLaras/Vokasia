using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
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
        services.AddIdentityCore<AppUser>(opt =>
            {
                opt.Password.RequiredLength = 8;
                opt.Password.RequireNonAlphanumeric = false;
                opt.Lockout.MaxFailedAccessAttempts = 5;
                opt.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                opt.User.RequireUniqueEmail = true;
            })
            .AddRoles<AppRole>()
            .AddSignInManager<SignInManager<AppUser>>()
            .AddEntityFrameworkStores<VokasiaDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<VokasiaClaimsFactory>();

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
            {
                options.LoginPath = "/account/login"; // form login sederhana H1; UI penuh H2-E2.
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
            });

        return services;
    }
}
