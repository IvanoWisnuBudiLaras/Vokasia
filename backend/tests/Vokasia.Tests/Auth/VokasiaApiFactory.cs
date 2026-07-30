extern alias ApiAssembly;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Tests.Auth;

/// <summary>
/// Test host untuk suite Auth (VOK-H1-E3). Mem-boot Program.cs sungguhan (OpenIddict, Identity,
/// controllers) tapi menukar VokasiaDbContext ke EF InMemory agar test tidak butuh Postgres/Docker
/// hidup — cukup untuk membuktikan konfigurasi OpenIddict & Identity, bukan pengganti integration
/// test Testcontainers penuh (itu wilayah H5-E3). extern alias ApiAssembly dipakai karena
/// Vokasia.Api & Vokasia.Worker sama-sama punya top-level-statement Program (CS0433 tanpa ini).
/// </summary>
public class VokasiaApiFactory : WebApplicationFactory<ApiAssembly::Program>
{
    public readonly string DbName = $"vokasia-auth-test-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // Test suites intentionally exercise many independent login flows from one loopback IP.
        // Keep the production default (20/IP) covered by the dedicated spray test, while avoiding
        // cross-test coupling in this shared integration host.
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:LoginAttemptsPerIp"] = "1000",
            }));
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
        });
        builder.ConfigureServices(services =>
        {
            // AddDbContext(Npgsql) di AddVokasiaInfrastructure mendaftarkan IDbContextOptionsConfiguration<T>
            // via TryAddEnumerable (ADITIF — dirancang agar beberapa delegate konfigurasi bisa digabung).
            // Kalau kita cuma AddDbContext(InMemory) lagi tanpa membuang yang lama, KEDUA delegate
            // (UseNpgsql + UseInMemoryDatabase) sama-sama dijalankan ke satu DbContextOptions saat
            // dibangun -> persis error "two database providers registered". Maka ketiganya wajib dibuang
            // dulu: DbContextOptions<T>, DbContextOptions (non-generic), IDbContextOptionsConfiguration<T>.
            var toRemove = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<VokasiaDbContext>) ||
                d.ServiceType == typeof(DbContextOptions) ||
                d.ServiceType == typeof(IDbContextOptionsConfiguration<VokasiaDbContext>))
                .ToList();
            foreach (var d in toRemove)
            {
                services.Remove(d);
            }

            services.AddDbContext<VokasiaDbContext>(opt => opt.UseInMemoryDatabase(DbName));
            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        });
    }
}
