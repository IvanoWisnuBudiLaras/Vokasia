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

    public static IServiceCollection AddVokasiaOpenIddict(this IServiceCollection services, IConfiguration config, IHostEnvironment env)
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
                       .SetEndSessionEndpointUris("/connect/logout")
                       .SetRevocationEndpointUris("/connect/revoke"); // VOK-H2-E3: BFF handleLogout revoke refresh instan (FR-AUTH-04)

                options.AllowAuthorizationCodeFlow()
                       .RequireProofKeyForCodeExchange()
                       .AllowRefreshTokenFlow();

                options.SetAccessTokenLifetime(TimeSpan.FromMinutes(15));
                options.SetRefreshTokenLifetime(TimeSpan.FromDays(14));
                options.UseReferenceRefreshTokens();

                // GAP ditemukan+ditambal sesi VOK-H2-E3 (DECISIONS.md D17): default OpenIddict
                // men-ENKRIPSI access token (JWE, 5 segmen: header.key.iv.ciphertext.tag), bukan
                // cuma menandatangani (JWS, 3 segmen: header.payload.signature) — ketahuan lewat
                // smoke test HTTP nyata: frontend decodeJwtPayload() (callback/route.ts) gagal
                // JSON.parse base64url segmen ke-2 ("Unexpected token" — itu ciphertext, bukan
                // JSON). Resource server (validation lokal, proses yg sama) tetap bisa dekripsi
                // krn share sertifikat yg sama; BFF (Next.js, proses TERPISAH, TANPA akses privat
                // key enkripsi apa pun) tidak bisa dan memang tidak seharusnya perlu — desain
                // AuthorizationController.GetDestinations() sudah eksplisit taruh sub/tenant_id
                // /role/name ke AccessToken utk DIBACA (bukan cuma dibawa) oleh BFF saat bikin
                // sesi. DisableAccessTokenEncryption() = access token JWS biasa (tertanda tangan,
                // TIDAK bisa dipalsu; TIDAK dienkripsi, siapa pun bisa baca claims — aman krn tak
                // ada rahasia di dalamnya, sama seperti kebanyakan resource-server JWT). Refresh
                // token TETAP reference/opaque (UseReferenceRefreshTokens di atas) — tidak
                // terpengaruh baris ini sama sekali.
                options.DisableAccessTokenEncryption();

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
                       // Revocation SENGAJA tanpa .EnableRevocationEndpointPassthrough(): tidak ada
                       // logika kustom yang dibutuhkan (beda dgn authorize/token/logout di atas yang
                       // butuh AuthorizationController) — OpenIddict menangani RFC 7009 secara native.

                if (env.IsDevelopment())
                {
                    // GAP ditemukan+ditambal sesi VOK-H2-E3 (DECISIONS.md D17): OpenIddict menolak
                    // SEMUA request /connect/* lewat HTTP polos dgn 400 "This server only accepts
                    // HTTPS requests" kecuali baris ini ada — memblokir SELURUH flow interaktif
                    // (bukan cuma BFF H2-E3; H1-E3 pun tak pernah benar2 dicoba lewat HTTP nyata,
                    // hanya lewat TestServer in-process yg tak menegakkan aturan transport yang
                    // sama). Dev/local jalan di http://localhost tanpa TLS (docker-compose pun blm
                    // ada reverse-proxy TLS) — HANYA didisable utk Development, produksi WAJIB HTTPS.
                    options.UseAspNetCore().DisableTransportSecurityRequirement();
                }
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

        // GAP ditemukan+ditambal sesi VOK-H2-E3 (DECISIONS.md D17): client BFF sudah dikasih
        // permission `scp:api` sejak H1-E3, tapi scope "api" itu SENDIRI tidak pernah didaftarkan
        // sbg OpenIddictScope — OpenIddict menolak scope apa pun yg diminta client (401/400
        // invalid_scope) kecuali dikenal scope manager, TERLEPAS dari permission client (permission
        // cuma bilang "client BOLEH minta scope ini KALAU scope-nya valid/dikenal"). "offline_access"
        // lolos tanpa registrasi krn itu reserved scope bawaan OpenIddict.
        var scopeManager = scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();
        if (await scopeManager.FindByNameAsync("api") is null)
        {
            await scopeManager.CreateAsync(new OpenIddictScopeDescriptor
            {
                Name = "api",
                DisplayName = "Akses API Vokasia",
                Resources = { "vokasia-api" },
            });
        }

        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

        var descriptor = new OpenIddictApplicationDescriptor
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
                Permissions.Endpoints.Revocation, // VOK-H2-E3: BFF handleLogout panggil /connect/revoke (FR-AUTH-04)
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.GrantTypes.RefreshToken,
                Permissions.ResponseTypes.Code,
                Permissions.Scopes.Profile,
                Permissions.Prefixes.Scope + "api",
                Permissions.Prefixes.Scope + "offline_access",
            },
            Requirements = { Requirements.Features.ProofKeyForCodeExchange },
        };

        var existing = await manager.FindByClientIdAsync(OpenIddictSetup.BffClientId);
        if (existing is null)
        {
            await manager.CreateAsync(descriptor);
            return;
        }

        // VOK-H2-E3 (DECISIONS.md D17): idempoten TAPI self-healing — client sudah ada dari sesi
        // H1-E3 tanpa permission Revocation (ditambah ticket ini). "return early" polos akan
        // membuat client lama diam-diam kekurangan permission baru selamanya (butuh hapus manual
        // DB tiap ticket auth berikutnya nambah scope/permission). UpdateAsync menyamakan ke
        // descriptor terbaru tiap startup — aman krn idempoten (deskriptor sama -> tanpa efek).
        await manager.UpdateAsync(existing, descriptor);
    }
}
