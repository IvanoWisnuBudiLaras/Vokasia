using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Minio;
using StackExchange.Redis;
using Vokasia.Domain.Common;
using Vokasia.Infrastructure.Email;
using Vokasia.Infrastructure.Messaging;
using Vokasia.Infrastructure.Persistence;
using Vokasia.Infrastructure.Security;
using Vokasia.Infrastructure.TenantContext;

namespace Vokasia.Infrastructure;

/// <summary>
/// Satu titik registrasi dependency infra murni: DbContext, Redis, MinIO client, tenant context.
/// Identity (AddIdentityCore dst.) SENGAJA TIDAK di sini — itu wilayah ENG-3, lihat
/// Vokasia.Api/Auth/IdentitySetup.cs (DECISIONS.md D14: pisahkan agar tidak dobel-registrasi).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddVokasiaInfrastructure(this IServiceCollection services, IConfiguration config, IHostEnvironment? env = null)
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
        services.AddSingleton<IBffSessionRevoker, BffSessionRevoker>();

        services.AddMinio(configureClient => configureClient
            .WithEndpoint(config["Minio:Endpoint"] ?? "localhost:9000")
            .WithCredentials(config["Minio:AccessKey"] ?? "vokasia", config["Minio:SecretKey"] ?? "vokasia_dev")
            .WithSSL(false)
            .Build());

        // VOK-H4-E1: IdempotencyGuard/INotifier butuh VokasiaDbContext scoped yang SAMA dgn
        // consumer/endpoint pemanggilnya (satu ChangeTracker, satu transaksi - lihat doc-comment
        // masing-masing).
        services.AddScoped<IdempotencyGuard>();
        services.AddScoped<INotifier, Notifier>();

        // VOK-H4-E3 §2: rantai IEmailSender - konkret (DevLog di Development, Smtp selainnya) DIBUNGKUS
        // IdempotentEmailSender (decorator, lihat doc-comment kelasnya) - HANYA hasil bungkusan yang
        // didaftarkan sbg IEmailSender publik; consumer/endpoint tak pernah lihat konkretnya langsung.
        // `env` opsional (default null -> anggap BUKAN Development) supaya caller lama (test yang
        // belum sempat diupdate) tetap compile - tapi Api/Worker Program.cs SELALU mengirim IHostEnvironment
        // nyata (lihat pemanggilan masing-masing).
        var isDevelopment = env?.IsDevelopment() ?? false;
        if (isDevelopment)
        {
            services.AddScoped<DevLogEmailSender>();
            services.AddScoped<IEmailSender>(sp => new IdempotentEmailSender(
                sp.GetRequiredService<VokasiaDbContext>(),
                sp.GetRequiredService<DevLogEmailSender>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<IdempotentEmailSender>>()));
        }
        else
        {
            services.AddScoped<SmtpEmailSender>();
            services.AddScoped<IEmailSender>(sp => new IdempotentEmailSender(
                sp.GetRequiredService<VokasiaDbContext>(),
                sp.GetRequiredService<SmtpEmailSender>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<IdempotentEmailSender>>()));
        }

        return services;
    }
}
