using Microsoft.AspNetCore.Identity;
using Minio;
using Minio.DataModel.Args;
using Vokasia.Api.Auth;
using Vokasia.Api.Auth.MagicLink;
using Vokasia.Api.Endpoints;
using Vokasia.Infrastructure;
using Vokasia.Infrastructure.Identity;
using Vokasia.Infrastructure.Persistence;
using Vokasia.Infrastructure.Seeding;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddVokasiaInfrastructure(builder.Configuration);

// Identity (SignInManager, UserManager, cookie scheme, claims factory) — satu titik di IdentitySetup.cs (VOK-H1-E3).
builder.Services.AddVokasiaIdentity();

builder.Services.AddVokasiaOpenIddict(builder.Configuration, builder.Environment);
builder.Services.AddVokasiaRbacPolicies(); // AddAuthorizationBuilder() di dalamnya — jangan tambah AddAuthorization() lagi.
builder.Services.AddControllers();
builder.Services.AddScoped<MagicLinkService>(); // VOK-H2-E3 §3

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>(); // AC VOK-H2-E3: isi ITenantContext sebelum endpoint apa pun jalan.
app.UseAuthorization();

app.MapControllers();
app.MapPeriodsEndpoints();
app.MapStudentsEndpoints();
app.MapCompaniesAndPlacementsEndpoints();
app.MapSchoolUsersEndpoints();
app.MapAccountEndpoints(); // VOK-H2-E3: tambal gap /account/login (LoginPath H1-E3, lihat DECISIONS.md D17)
app.MapAuditEndpoints(); // VOK-H2-E3 §2: WriteAuditLog — dipanggil BFF (TokenReuseDetected dst.)
app.MapMagicLinkEndpoints(); // VOK-H2-E3 §3: create+validate magic link mentor
app.MapJournalEndpoints(); // VOK-H3-E1: siklus jurnal siswa/mentor/guru

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
