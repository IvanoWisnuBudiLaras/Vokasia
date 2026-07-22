using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Minio;
using StackExchange.Redis;
using Vokasia.Domain.Common;
using Vokasia.Infrastructure.Messaging;
using Vokasia.Infrastructure.Persistence;
using Vokasia.Infrastructure.TenantContext;

namespace Vokasia.Infrastructure;

/// <summary>
/// Satu titik registrasi dependency infra murni: DbContext, Redis, MinIO client, tenant context.
/// Identity (AddIdentityCore dst.) SENGAJA TIDAK di sini — itu wilayah ENG-3, lihat
/// Vokasia.Api/Auth/IdentitySetup.cs (DECISIONS.md D14: pisahkan agar tidak dobel-registrasi).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddVokasiaInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        // AC VOK-H2-E3: AmbientTenantContext didaftar SEBAGAI kelas konkret scoped (bukan cuma
        // interface) agar TenantResolutionMiddleware (Vokasia.Api) & VokasiaDbContext berbagi
        // INSTANCE SAMA per-request — middleware set nilai, DbContext baca nilai yg sama itu.
        services.AddScoped<AmbientTenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<AmbientTenantContext>());

        services.AddDbContext<VokasiaDbContext>(opt =>
            opt.UseNpgsql(config.GetConnectionString("Default")
                ?? config["ConnectionStrings:Default"]
                ?? "Host=localhost;Port=5432;Database=vokasia;Username=vokasia;Password=vokasia_dev"));

        var redisConn = config["Redis:Connection"] ?? "localhost:6379";
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConn));

        services.AddMinio(configureClient => configureClient
            .WithEndpoint(config["Minio:Endpoint"] ?? "localhost:9000")
            .WithCredentials(config["Minio:AccessKey"] ?? "vokasia", config["Minio:SecretKey"] ?? "vokasia_dev")
            .WithSSL(false)
            .Build());

        // VOK-H4-E1: IdempotencyGuard/INotifier butuh VokasiaDbContext scoped yang SAMA dgn
        // consumer/endpoint pemanggilnya (satu ChangeTracker, satu transaksi - lihat doc-comment
        // masing-masing). IEmailSender dev-only (log) sampai infra SMTP/Resend H4-E3.
        services.AddScoped<IdempotencyGuard>();
        services.AddScoped<INotifier, Notifier>();
        services.AddScoped<IEmailSender, LoggingEmailSender>();

        return services;
    }
}
