using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Minio;
using Minio.DataModel.Args;
using Vokasia.Api.Auth;
using Vokasia.Api.Auth.MagicLink;
using Vokasia.Api.Endpoints;
using Vokasia.Api.Middleware;
using Vokasia.Api.RateLimiting;
using Vokasia.Infrastructure;
using Vokasia.Infrastructure.Identity;
using Vokasia.Infrastructure.Persistence;
using Vokasia.Infrastructure.Seeding;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddVokasiaInfrastructure(builder.Configuration, builder.Environment);

// Identity (SignInManager, UserManager, cookie scheme, claims factory) — satu titik di IdentitySetup.cs (VOK-H1-E3).
builder.Services.AddVokasiaIdentity();

builder.Services.AddVokasiaOpenIddict(builder.Configuration, builder.Environment);
builder.Services.AddVokasiaRbacPolicies(); // AddAuthorizationBuilder() di dalamnya — jangan tambah AddAuthorization() lagi.
builder.Services.AddControllers();
builder.Services.AddScoped<MagicLinkService>(); // VOK-H2-E3 §3

// VOK-H3-E3 §1: DomainImmutableException -> 409 {code,message} (bukan 500 generik) + ProblemDetails
// bawaan framework utk exception lain yang tak sengaja lolos (tetap format JSON konsisten, bukan
// halaman HTML dev-exception-page di Production).
builder.Services.AddExceptionHandler<DomainImmutableExceptionHandler>();
builder.Services.AddExceptionHandler<QuotaExceededExceptionHandler>(); // VOK-H6-E1 §5 (FR-BIL-03)
builder.Services.AddProblemDetails();

// VOK-H3-E3 §2: FluentValidation — semua Validator di assembly ini (Vokasia.Api) otomatis terdaftar
// sbg IValidator<T> DI, dibaca ValidationFilter (Endpoints/*.cs) per request type. Assembly-scan
// (bukan daftar manual satu-satu) supaya validator baru otomatis kepakai tanpa sentuh Program.cs lagi.
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// VOK-H3-E3 §3: rate limit login (/connect/token) + endpoint publik (/api/mentor-invites/validate).
builder.Services.AddVokasiaRateLimiting();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// VOK-H3-E3 §1: paling awal — tangkap exception dari SEMUA middleware/endpoint di bawahnya.
app.UseExceptionHandler();

app.UseHttpsRedirection();
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

// Seed client OAuth BFF — idempoten, aman tiap startup (VOK-H1-E3).
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

// CLI hook VOK-H2-E1: `dotnet run --project src/Vokasia.Api -- seed demo` — 1 perintah dari clean
// state (NFR-MNT-04), tanpa menjalankan web server (keluar setelah selesai).
if (args is ["seed", "demo", ..])
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    var sw = System.Diagnostics.Stopwatch.StartNew();
    var result = await DemoSeeder.SeedDemoDataAsync(db, userManager);
    sw.Stop();
    Console.WriteLine($"[seed demo] {result} ({sw.Elapsed.TotalSeconds:F1}s)");
    return;
}

app.Run();

public partial class Program { } // agar Vokasia.Tests bisa pakai WebApplicationFactory<Program>
