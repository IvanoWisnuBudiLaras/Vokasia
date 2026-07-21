using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Vokasia.Api.Auth;

/// <summary>
/// Registrasi OpenIddict server (FR-AUTH-01/02). PKCE WAJIB — RequireProofKeyForCodeExchange
/// tidak boleh dihapus. Access token 15 menit (NFR-SEC-01); refresh sliding 14 hari, rotasi
/// penuh ditegakkan sisi BFF/Redis di H2-E3 (server ini hanya menerbitkan, tidak menyimpan
/// token browser-side).
/// </summary>
public static class OpenIddictSetup
{
    public const string BffClientId = "vokasia-bff";

    public static IServiceCollection AddVokasiaOpenIddict(this IServiceCollection services, IConfiguration config)
    {
        services.AddOpenIddict()
            .AddCore(options =>
            {
                options.UseEntityFrameworkCore()
                    .UseDbContext<Vokasia.Infrastructure.Persistence.VokasiaDbContext>();
            })
            .AddServer(options =>
            {
                options.SetAuthorizationEndpointUris("/connect/authorize")
                       .SetTokenEndpointUris("/connect/token")
                       .SetEndSessionEndpointUris("/connect/logout");

                options.AllowAuthorizationCodeFlow()
                       .RequireProofKeyForCodeExchange()
                       .AllowRefreshTokenFlow();

                options.SetAccessTokenLifetime(TimeSpan.FromMinutes(15));
                options.SetRefreshTokenLifetime(TimeSpan.FromDays(14));
                options.UseReferenceRefreshTokens();

                var issuer = config["OpenIddict:Issuer"];
                if (!string.IsNullOrWhiteSpace(issuer))
                {
                    options.SetIssuer(new Uri(issuer));
                }

                // Dev: kunci ephemeral (regenerasi tiap restart — TIDAK untuk produksi).
                // Prod: ganti dengan sertifikat X.509 dari env/secret store (NFR-SEC-07).
                options.AddDevelopmentEncryptionCertificate()
                       .AddDevelopmentSigningCertificate();

                options.UseAspNetCore()
                       .EnableAuthorizationEndpointPassthrough()
                       .EnableTokenEndpointPassthrough()
                       .EnableEndSessionEndpointPassthrough();
            })
            .AddValidation(options =>
            {
                options.UseLocalServer();
                options.UseAspNetCore();
            });

        return services;
    }

    /// <summary>
    /// Registrasi client BFF (confidential) — idempoten, aman dipanggil tiap startup.
    /// Redirect URI dev: frontend BFF callback route (dibangun H2-E3).
    /// </summary>
    public static async Task SeedOAuthClientsAsync(IServiceProvider sp)
    {
        using var scope = sp.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

        if (await manager.FindByClientIdAsync(OpenIddictSetup.BffClientId) is not null)
        {
            return; // sudah ada — idempoten.
        }

        await manager.CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = OpenIddictSetup.BffClientId,
            ClientType = ClientTypes.Confidential,
            ClientSecret = Environment.GetEnvironmentVariable("OIDC_BFF_CLIENT_SECRET") ?? "dev-only-secret-change-me",
            ConsentType = ConsentTypes.Implicit,
            RedirectUris = { new Uri("http://localhost:3000/api/auth/callback") },
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Token,
                Permissions.Endpoints.EndSession,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.GrantTypes.RefreshToken,
                Permissions.ResponseTypes.Code,
                Permissions.Scopes.Profile,
                Permissions.Prefixes.Scope + "api",
                Permissions.Prefixes.Scope + "offline_access",
            },
            Requirements = { Requirements.Features.ProofKeyForCodeExchange },
        });
    }
}
