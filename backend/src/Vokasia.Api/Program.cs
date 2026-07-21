using Vokasia.Api.Auth;
using Vokasia.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddVokasiaInfrastructure(builder.Configuration);

// Identity (SignInManager, UserManager, cookie scheme, claims factory) — satu titik di IdentitySetup.cs (VOK-H1-E3).
builder.Services.AddVokasiaIdentity();

builder.Services.AddVokasiaOpenIddict(builder.Configuration);
builder.Services.AddAuthorization();
builder.Services.AddControllers();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Smoke endpoint H1 — dibuktikan compose+migration hidup end-to-end (gate M0).
app.MapGet("/health/ping", () => Results.Ok(new { status = "ok", service = "Vokasia.Api" }));

// Seed client OAuth BFF — idempoten, aman tiap startup (VOK-H1-E3).
await OpenIddictSetup.SeedOAuthClientsAsync(app.Services);

app.Run();

public partial class Program { } // agar Vokasia.Tests bisa pakai WebApplicationFactory<Program>
