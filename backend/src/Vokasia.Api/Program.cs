using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using System.Text.Json;
using Minio;
using Minio.DataModel.Args;
using Microsoft.EntityFrameworkCore;
using Vokasia.Api.Auth;
using Vokasia.Api.Auth.MagicLink;
using Vokasia.Api.Endpoints;
using Vokasia.Api.Middleware;
using Vokasia.Api.RateLimiting;
using Vokasia.Api.Storage;
using Vokasia.Infrastructure;
using Vokasia.Infrastructure.Identity;
using Vokasia.Infrastructure.Persistence;
using Vokasia.Infrastructure.Seeding;

var builder = WebApplication.CreateBuilder(args);
var developmentLikeEnvironment = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing");

var dataProtection = builder.Services.AddDataProtection()
    .SetApplicationName("Vokasia.Api");
var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"];
if (!developmentLikeEnvironment)
{
    if (string.IsNullOrWhiteSpace(dataProtectionKeysPath))
    {
        throw new InvalidOperationException(
            "DataProtection:KeysPath wajib diisi di Production agar cookie/anti-forgery keys persisten.");
    }

    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
    var dataProtectionCertificatePath = builder.Configuration["DataProtection:CertificatePath"];
    if (string.IsNullOrWhiteSpace(dataProtectionCertificatePath) || !File.Exists(dataProtectionCertificatePath))
    {
        throw new InvalidOperationException(
            "DataProtection:CertificatePath wajib menunjuk ke sertifikat PFX yang valid di Production.");
    }

    var dataProtectionCertificate = X509CertificateLoader.LoadPkcs12FromFile(
        dataProtectionCertificatePath,
        builder.Configuration["DataProtection:CertificatePassword"],
        X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.MachineKeySet);
    dataProtection.ProtectKeysWithCertificate(dataProtectionCertificate);
}
else if (!string.IsNullOrWhiteSpace(dataProtectionKeysPath))
{
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
}

builder.Services.AddOpenApi();
builder.Services.AddVokasiaInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddSingleton<IBrowserObjectStorageSigner, BrowserObjectStorageSigner>();

// Identity (SignInManager, UserManager, cookie scheme, claims factory) — satu titik di IdentitySetup.cs (VOK-H1-E3).
builder.Services.AddVokasiaIdentity();

builder.Services.AddVokasiaOpenIddict(builder.Configuration, builder.Environment);
builder.Services.AddVokasiaRbacPolicies(); // AddAuthorizationBuilder() di dalamnya – jangan tambah AddAuthorization() lagi.
builder.Services.AddControllers();
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ??
    (developmentLikeEnvironment ? ["http://localhost:3000"] : []);
if (!developmentLikeEnvironment && corsOrigins.Any(string.IsNullOrWhiteSpace))
{
    throw new InvalidOperationException("Cors:AllowedOrigins wajib berisi origin production yang eksplisit.");
}
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy =>
{
    policy.WithOrigins(corsOrigins.Where(origin => !string.IsNullOrWhiteSpace(origin)).ToArray())
        .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
        .WithHeaders("Content-Type", "Authorization", "Accept");
}));
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "vok_antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});
builder.Services.AddScoped<MagicLinkService>(); // VOK-H2-E3 §3

// VOK-H7-E1: Tambahkan ASP.NET Core Health Checks bawaan. Kita gunakan custom ping sederhana untuk database, redis, dan rabbitmq agar tidak menambah dependencies NuGet baru (AGENTS.md #13).
builder.Services.AddHealthChecks()
    .AddCheck<PostgresHealthCheck>("postgres")
    .AddCheck<RedisHealthCheck>("redis");

// VOK-H3-E3 §1: DomainImmutableException -> 409 {code,message} (bukan 500 generik) + ProblemDetails
// bawaan framework utk exception lain yang tak sengaja lolos (tetap format JSON konsisten, bukan
// halaman HTML dev-exception-page di Production).
builder.Services.AddExceptionHandler<DomainImmutableExceptionHandler>();
builder.Services.AddExceptionHandler<QuotaExceededExceptionHandler>(); // VOK-H6-E1 §5 (FR-BIL-03)
builder.Services.AddProblemDetails();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
    ForwardedHeadersSetup.Configure(options, builder.Configuration));

// VOK-H3-E3 §2: FluentValidation — semua Validator di assembly ini (Vokasia.Api) otomatis terdaftar
// sbg IValidator<T> DI, dibaca ValidationFilter (Endpoints/*.cs) per request type. Assembly-scan
// (bukan daftar manual satu-satu) supaya validator baru otomatis kepakai tanpa sentuh Program.cs lagi.
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// VOK-H3-E3 §3: rate limit login (/connect/token) + endpoint publik (/api/mentor-invites/validate).
builder.Services.AddVokasiaRateLimiting(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// VOK-H3-E3 §1: paling awal — tangkap exception dari SEMUA middleware/endpoint di bawahnya.
app.UseExceptionHandler();
// Trust X-Forwarded-* only from explicitly configured proxy addresses/networks. This must run
// before HTTPS redirection, authentication and rate limiting so scheme/client-IP decisions use
// the original request values supplied by the trusted reverse proxy.
app.UseForwardedHeaders();

// VOK-H6-E3 §3 (NFR-SEC-07): HSTS HANYA non-Development (rekomendasi resmi ASP.NET Core — di
// localhost http, HSTS bikin browser "mengingat" paksa-https utk domain lokal, menyulitkan dev
// berikutnya. Produksi WAJIB HTTPS, jadi HSTS aman & diinginkan di sana).
if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
{
    app.UseHsts();
}
app.UseMiddleware<Vokasia.Api.Middleware.SecurityHeadersMiddleware>(); // nosniff/CSP dasar/X-Frame-Options/Referrer-Policy — SEMUA response, termasuk publik & error.

app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("Frontend");
app.UseStatusCodePages(async statusContext =>
{
    var context = statusContext.HttpContext;
    if (context.Response.StatusCode is not (StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden or StatusCodes.Status404NotFound) ||
        !ApiStatusCodePages.ShouldWriteJson(context))
    {
        return;
    }

    context.Response.ContentType = "application/problem+json; charset=utf-8";
    await context.Response.WriteAsync(JsonSerializer.Serialize(ApiStatusCodePages.CreatePayload(context)));
});
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>(); // AC VOK-H2-E3: isi ITenantContext sebelum endpoint apa pun jalan.
app.UseAuthorization();

// VOK-H3-E3 §3: pre-buffer form body ASYNC sebelum UseRateLimiter() - partition-key selector
// kebijakan "login" (VokasiaRateLimiting.cs) baca httpContext.Request.Form["email"] secara SINKRON;
// tanpa pre-read async di sini, akses sync thd body yang belum pernah dibaca BISA throw
// "Synchronous operations are disallowed" di Kestrel produksi (AllowSynchronousIO=false default -
// TIDAK terlihat lewat TestServer yang dipakai test, kelas gap yang berulang kali ditemukan sesi
// ini). Form yang sudah di-cache di sini aman dibaca ulang PostLogin (AccountEndpoints.cs) tanpa
// re-read stream. No-op (murah) utk request tanpa form-content-type (mayoritas: JSON API).
app.Use(async (context, next) =>
{
    if (context.Request.HasFormContentType)
    {
        await context.Request.ReadFormAsync();
    }
    await next();
});

app.UseRateLimiter(); // VOK-H3-E3 §3: setelah UseAuthorization - policy per-endpoint via [EnableRateLimiting]/RequireRateLimiting.

app.MapControllers();
app.MapPeriodsEndpoints();
app.MapStudentsEndpoints();
app.MapCompaniesAndPlacementsEndpoints();
app.MapSchoolUsersEndpoints();
app.MapAccountEndpoints(); // VOK-H2-E3: tambal gap /account/login (LoginPath H1-E3, lihat DECISIONS.md D17)
app.MapAuditEndpoints(); // VOK-H2-E3 §2: WriteAuditLog — dipanggil BFF (TokenReuseDetected dst.)
app.MapImpersonationEndpoints(); // VOK-H6-E3 §1: EndImpersonation (StartImpersonation ada di AuthorizationController.Exchange())
app.MapMagicLinkEndpoints(); // VOK-H2-E3 §3: create+validate magic link mentor
app.MapJournalEndpoints(); // VOK-H3-E1: siklus jurnal siswa/mentor/guru
app.MapNotificationEndpoints(); // VOK-H4-E1 §4: bell notifikasi in-app lintas peran
app.MapDashboardEndpoints(); // VOK-H4-E1 §4: GetSchoolDashboard (W3)
app.MapVisitEndpoints(); // VOK-H5-E1 §1: kunjungan monitoring guru ke DUDI
app.MapRubricEndpoints(); // VOK-H5-E1 §2: template rubrik penilaian
app.MapAssessmentEndpoints(); // VOK-H5-E1 §3: skor dua sisi + finalisasi
app.MapGradeRecapEndpoints(); // VOK-H5-E1 §4: rekap nilai + export async
app.MapCertificateEndpoints(); // VOK-H5-E1 §5: unduh sertifikat + verifikasi publik
app.MapSaTenantsEndpoints(); // VOK-H6-E1 §1: /sa/tenants — wizard provisioning + CRUD
app.MapSaPlansEndpoints(); // VOK-H6-E1 §3: /sa/plans — paket langganan (minimal, flags menyusul)
app.MapPortfolioEndpoints(); // VOK-H6-E1 §6: portofolio siswa + /p/{slug} publik
app.MapBillingEndpoints(); // VOK-H6-E1 §5: invoice (SA semua + confirm, TenantAdmin miliknya + proof)
app.MapSaCompaniesEndpoints(); // VOK-H6-E1 §2: /sa/companies — registry DUDI global + merge
app.MapSaOpsEndpoints(); // VOK-H6-E1 §4: KPI platform + kesehatan sistem + audit log (SA)

// Smoke endpoint H1 — dibuktikan compose+migration hidup end-to-end (gate M0).
app.MapGet("/health/ping", () => Results.Ok(new { status = "ok", service = "Vokasia.Api" }));

// VOK-H7-E1: Map endpoint /health bawaan ASP.NET Core
app.MapHealthChecks("/health");

// CLI hook is development-only. A production process must never populate demo tenants by
// accident, even when an operator passes the argument to the deployed binary.
if (args is ["seed", "demo", ..] && !app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
{
    throw new InvalidOperationException("seed demo hanya boleh dijalankan saat ASPNETCORE_ENVIRONMENT=Development atau Testing.");
}

// Seed client OAuth BFF — idempoten, aman tiap startup (VOK-H1-E3).
// VOK-H8: Apply pending EF migrations BEFORE seeding - Postgres volume yg sudah ada dari
// build sebelumnya (sebelum gate ini dipasang) mungkin belum punya OpenIddictScopes/OutboxMessages
// dst. MigrateAsync idempoten (no-op kalau skema sudah up-to-date).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
    if (db.Database.IsRelational())
    {
        await db.Database.MigrateAsync();
    }
}

await OpenIddictSetup.SeedOAuthClientsAsync(app.Services);

// VOK-H3-E1: ensure bucket foto jurnal ada SEKALI saat startup (bukan per-request presign, hindari
// latency HEAD-bucket tiap panggilan) - idempoten, aman dipanggil tiap restart.
//
// GAP ditemukan+ditambal Gate M0 redeploy (DECISIONS.md D24): blok ini SEMPAT membuat SELURUH API
// crash saat startup (exit 139, docker logs kosong tanpa stack trace) krn endpoint Minio salah
// (fallback "localhost:9000" tak nyambung dari DALAM container api sendiri ke container minio
// terpisah - sudah diperbaiki di docker-compose.yml, Minio__Endpoint=minio:9000). Try/catch di sini
// SENGAJA ditambahkan sbg lapis pertahanan KEDUA: MinIO tak bisa dihubungi (mis. env lain yang
// belum diset benar, container minio down sementara, dst.) TIDAK BOLEH menjatuhkan SELURUH API
// (login/jurnal-teks/approval semuanya tetap harus jalan) - cukup log warning, endpoint upload-url
// akan gagal sendiri belakangan kalau memang MinIO benar2 tak tersedia (kegagalan terlokalisir,
// bukan downtime total).
try
{
    using var scope = app.Services.CreateScope();
    var minio = scope.ServiceProvider.GetRequiredService<IMinioClient>();
    var bucket = app.Configuration["Minio:Bucket"] ?? "vokasia-journal";
    var exists = await minio.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucket));
    if (!exists)
    {
        await minio.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucket));
    }
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Gagal ensure bucket MinIO saat startup - endpoint upload foto jurnal mungkin tak berfungsi sampai MinIO tersedia.");
}

// CLI hook VOK-H2-E1: `dotnet run --project src/Vokasia.Api -- seed demo` / `seed reset`
if (args is ["seed", ..])
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    var sw = System.Diagnostics.Stopwatch.StartNew();
    var forceReset = args.Contains("reset") || args.Contains("--force");
    var result = await DemoSeeder.SeedDemoDataAsync(db, userManager, forceReset: forceReset);
    sw.Stop();
    Console.WriteLine($"[seed demo] {result} ({sw.Elapsed.TotalSeconds:F1}s)");
    return;
}

app.Run();

public partial class Program { } // agar Vokasia.Tests bisa pakai WebApplicationFactory<Program>

internal sealed class PostgresHealthCheck(
    IServiceScopeFactory scopeFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            return await db.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Gagal terhubung ke Postgres.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Pemeriksaan Postgres gagal.",
                ex);
        }
    }
}

internal sealed class RedisHealthCheck(
    StackExchange.Redis.IConnectionMultiplexer multiplexer) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await multiplexer.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Pemeriksaan Redis gagal.",
                ex);
        }
    }
}
