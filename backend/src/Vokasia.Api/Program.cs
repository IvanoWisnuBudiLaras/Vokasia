using Microsoft.AspNetCore.Identity;
using Vokasia.Api.Auth;
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

builder.Services.AddVokasiaOpenIddict(builder.Configuration);
builder.Services.AddVokasiaRbacPolicies(); // AddAuthorizationBuilder() di dalamnya — jangan tambah AddAuthorization() lagi.
builder.Services.AddControllers();

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

// Smoke endpoint H1 — dibuktikan compose+migration hidup end-to-end (gate M0).
app.MapGet("/health/ping", () => Results.Ok(new { status = "ok", service = "Vokasia.Api" }));

// Seed client OAuth BFF — idempoten, aman tiap startup (VOK-H1-E3).
await OpenIddictSetup.SeedOAuthClientsAsync(app.Services);

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
