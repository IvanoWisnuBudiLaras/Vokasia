using OpenIddict.Abstractions;
using System.Security.Cryptography.X509Certificates;
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

    /// <summary>
    /// Grant kustom VOK-H2-E3 §3 (magic link mentor) — mentor tak punya password (FR-AUTH-03),
    /// jadi tak bisa lewat authorization_code (yang butuh cookie login/AuthorizationController
    /// .Authorize() lolos AuthenticateAsync Cookies). Ditangani AuthorizationController.Exchange()
    /// sbg cabang baru: validasi+konsumsi token magic link (MagicLinkService), lalu terbitkan
    /// identity persis sama (VokasiaClaimsFactory) spt grant lain — access/refresh token yang
    /// dihasilkan TIDAK berbeda sama sekali dari flow OAuth normal (satu jalur penerbitan token,
    /// bukan jalur sesi paralel ad-hoc — pelajaran D17 soal bahaya default-scheme diam-diam).
    /// </summary>
    public const string MagicLinkGrantType = "urn:vokasia:params:oauth:grant-type:magic-link";

    /// <summary>
    /// Grant kustom VOK-H6-E3 §1 (StartImpersonation, FR-AUTH-07) — SuperAdmin MENUKAR access
    /// token miliknya SENDIRI (dikirim sbg Authorization: Bearer di request /connect/token ini,
    /// dibaca dari HttpContext.User yang SUDAH diisi UseAuthentication() sebelum controller ini
    /// jalan — TIDAK butuh [Authorize] di controller: middleware auth berjalan utk SEMUA request,
    /// terlepas ada/tidaknya atribut itu, lihat Program.cs urutan middleware) dengan access token
    /// BARU ber-identitas user TARGET penuh (role/tenant_id/sub semua milik target), PLUS 1 claim
    /// tambahan "impersonator_id" = sub SA asli. Ditangani
    /// AuthorizationController.Exchange() sbg cabang baru, identity dari VokasiaClaimsFactory yang
    /// SAMA (satu jalur penerbitan token, bukan sesi ad-hoc paralel — pelajaran D17 yang sama
    /// dgn MagicLinkGrantType di atas).
    /// </summary>
    public const string ImpersonationGrantType = "urn:vokasia:params:oauth:grant-type:impersonation";

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
                       .AllowRefreshTokenFlow()
                       .AllowCustomFlow(MagicLinkGrantType)
                       .AllowCustomFlow(ImpersonationGrantType);

                options.SetAccessTokenLifetime(TimeSpan.FromMinutes(15));
                options.SetRefreshTokenLifetime(TimeSpan.FromDays(14));
                options.UseReferenceRefreshTokens();

                // GAP ditemukan+ditambal sesi VOK-H2-E3 (DECISIONS.md D22): rolling refresh token
                // (redeem token lama + terbitkan token baru tiap refresh) SUDAH default sejak
                // OpenIddict 3.0+ — TAPI ada "reuse leeway" default 30 DETIK (didesain resmi utk
                // toleransi request konkuren/retry jaringan, lihat komentar maintainer kevinchalet di
                // openiddict-core#1274): dalam jendela itu, token yg BARU SAJA diredeem tetap diterima
                // dipakai ulang (200 OK), bukan ditolak. Ketahuan lewat
                // RefreshRotationTests.OldRefreshToken_AfterRotation_IsRejected (Security/): reuse
                // langsung sesudah rotasi pertama (dalam hitungan milidetik, jauh di bawah 30 detik)
                // ternyata TETAP 200 OK — awalnya dikira bug "rotasi tak jalan", ternyata memang
                // perilaku leeway yg terdokumentasi, BUKAN kegagalan konfigurasi UseReferenceRefreshTokens.
                // AC VOK-H2-E3 §4 minta reuse lama -> DITOLAK (memicu revokeAllSessionsForUser di
                // frontend/src/lib/refresh.ts) — model ancaman proyek ini (curi token lama, replay stlh
                // korban sudah lanjut ke token baru) TIDAK butuh toleransi 30 detik itu (beda dari kasus
                // retry-jaringan yg leeway ini memang ditujukan utk). SetRefreshTokenReuseLeeway(Zero)
                // = tolak reuse SEKETIKA, sesuai model ancaman AC, mengorbankan toleransi retry-konkuren
                // (trade-off SADAR, bukan default yg tak diperiksa). Diverifikasi PROMPT D: leeway
                // default (baris ini dihapus) -> test merah (200 OK) -> pasang Zero -> hijau (400).
                options.SetRefreshTokenReuseLeeway(TimeSpan.Zero);

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
                if (env.IsEnvironment("Testing"))
                {
                    // Test hosts must not depend on the developer certificate store (which is
                    // unavailable in CI/sandboxed Windows runners).
                    options.AddEphemeralEncryptionKey()
                           .AddEphemeralSigningKey();
                }
                else if (env.IsDevelopment())
                {
                    // Development certificates are intentionally ephemeral and must never be
                    // used by a production process.
                    options.AddDevelopmentEncryptionCertificate()
                           .AddDevelopmentSigningCertificate();
                }
                else
                {
                    // Production keys come from a mounted secret/certificate store and survive
                    // restarts. Falling back to development certificates would invalidate every
                    // outstanding token after a restart.
                    options.AddEncryptionCertificate(LoadCertificate(config, "OpenIddict:EncryptionCertificatePath"))
                           .AddSigningCertificate(LoadCertificate(config, "OpenIddict:SigningCertificatePath"));
                }

                options.UseAspNetCore()
                       .EnableAuthorizationEndpointPassthrough()
                       .EnableTokenEndpointPassthrough()
                       .EnableEndSessionEndpointPassthrough();
                       // Revocation SENGAJA tanpa .EnableRevocationEndpointPassthrough(): tidak ada
                       // logika kustom yang dibutuhkan (beda dgn authorize/token/logout di atas yang
                       // butuh AuthorizationController) — OpenIddict menangani RFC 7009 secara native.

                if (env.IsDevelopment() || env.IsEnvironment("Testing"))
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
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var environment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();

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

        var developmentLike = environment.IsDevelopment() || environment.IsEnvironment("Testing");
        var frontendUrl = configuration["Frontend:PublicUrl"] ??
            (developmentLike ? "http://localhost:3000" : null);
        if (!Uri.TryCreate(frontendUrl, UriKind.Absolute, out var frontendUri) ||
            (!developmentLike && frontendUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                "Frontend:PublicUrl harus berupa URL HTTPS absolut di Production.");
        }

        var clientSecret = configuration["OpenIddict:BffClientSecret"] ??
            Environment.GetEnvironmentVariable("OIDC_BFF_CLIENT_SECRET");
        if (string.IsNullOrWhiteSpace(clientSecret))
        {
            if (!developmentLike)
            {
                throw new InvalidOperationException(
                    "OpenIddict:BffClientSecret/OIDC_BFF_CLIENT_SECRET wajib di Production.");
            }

            clientSecret = "dev-only-secret-change-me";
        }

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = OpenIddictSetup.BffClientId,
            ClientType = ClientTypes.Confidential,
            ClientSecret = clientSecret,
            ConsentType = ConsentTypes.Implicit,
            RedirectUris = { new Uri(frontendUri, "/api/auth/callback") },
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Token,
                Permissions.Endpoints.EndSession,
                Permissions.Endpoints.Revocation, // VOK-H2-E3: BFF handleLogout panggil /connect/revoke (FR-AUTH-04)
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.GrantTypes.RefreshToken,
                Permissions.Prefixes.GrantType + MagicLinkGrantType, // VOK-H2-E3 §3
                Permissions.Prefixes.GrantType + ImpersonationGrantType, // VOK-H6-E3 §1
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

    private static X509Certificate2 LoadCertificate(IConfiguration config, string pathKey)
    {
        var path = config[pathKey];
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException($"{pathKey} wajib diisi di Production.");
        }

        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"Sertifikat OpenIddict tidak ditemukan: {path}");
        }

        var passwordKey = pathKey.Replace("Path", "Password", StringComparison.Ordinal);
        return X509CertificateLoader.LoadPkcs12FromFile(
            path,
            config[passwordKey],
            X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.MachineKeySet);
    }
}
