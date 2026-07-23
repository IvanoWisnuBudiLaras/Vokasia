extern alias ApiAssembly;

using System.Net.Http.Headers;
using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure;
using Vokasia.Infrastructure.Identity;
using Vokasia.Infrastructure.Messaging;
using Vokasia.Infrastructure.Persistence;
using Vokasia.Infrastructure.TenantContext;
using Vokasia.Tests.Auth;
using Vokasia.Worker.Consumers;

namespace Vokasia.Tests.Integration;

/// <summary>
/// VOK-H5-E3 §0 Fondasi — boot API sungguhan (WebApplicationFactory&lt;Program&gt;, endpoint+
/// OpenIddict+RBAC+FluentValidation, config PERSIS Program.cs) + host Worker terpisah (SEMUA
/// consumer produksi + OutboxDispatcher, wiring identik Vokasia.Worker/Program.cs MINUS Hangfire/
/// jadwal cron — cron "dipicu manual" via <see cref="TriggerOpenAssessmentPhaseAsync"/>/
/// <see cref="TriggerEnqueueCertificateBatchAsync"/>, sesuai AC ticket) — SATU Postgres + SATU
/// RabbitMQ Testcontainers dipakai BERSAMA kedua host (mirror pola AsyncTestFixture H4-E3: 2 proses
/// logis, 1 broker/DB fisik).
///
/// [DEVIASI dicatat — lihat DECISIONS.md D36] Ticket literal minta 4 container Testcontainers
/// (Postgres+RabbitMQ+Redis+MinIO). Redis &amp; MinIO DI SINI memakai container docker-compose yang
/// SUDAH JALAN (localhost:6379 / localhost:9000, kredensial .env), BUKAN Testcontainers terpisah:
/// (1) AGENTS.md #13 melarang dependency NuGet baru tanpa persetujuan Developer eksplisit —
/// Testcontainers.Redis/Testcontainers.Minio belum direferensi proyek ini sama sekali. (2) Redis:
/// dikonfirmasi via grep — `IConnectionMultiplexer` HANYA direferensi di titik registrasi
/// (`Vokasia.Infrastructure/DependencyInjection.cs`), TAK PERNAH benar2 dibaca/ditulis di mana pun
/// kode backend .NET (sesi BFF Redis murni domain Next.js/Node, di luar cakupan proses dotnet test
/// ini) — container Testcontainers terpisah tak menambah cakupan pembuktian apa pun dibanding
/// container dev yang sudah hidup. (3) MinIO: object storage murni (bukan broker pesan, tak ada
/// konsumsi bersama yang bisa rebutan) — risiko tabrakan dgn data dev NOL (kunci objek per test
/// diprefiks GUID acak: `tenant/{tenantId}/...`), aman dipakai bersama proses dev tanpa
/// pembersihan. Postgres &amp; RabbitMQ TETAP Testcontainers murni (isolasi WAJIB): migrasi bersih
/// per run, dan RabbitMQ compose SUDAH dikonsumsi worker container produksi sungguhan yang jalan
/// paralel — memakai broker yang sama akan menyebabkan REBUTAN pesan antara consumer test vs
/// consumer produksi (message stealing, hasil test jadi tak deterministik).
///
/// [PRASYARAT] Suite ini butuh docker-compose stack dev SUDAH JALAN (redis+minio minimal) di
/// localhost:6379/9000 kredensial .env root — BUKAN cuma Docker Desktop hidup. Kalau compose belum
/// up, InitializeAsync tetap start Postgres+RabbitMQ Testcontainers (independen), tapi endpoint yang
/// menyentuh MinIO (upload foto, sertifikat) akan gagal konek — dicatat sbg batasan lingkungan test,
/// BUKAN bug kode.
/// </summary>
public class VokasiaIntegrationFactory : WebApplicationFactory<ApiAssembly::Program>, IAsyncLifetime
{
    private const string MinioEndpoint = "localhost:9000";
    private const string MinioAccessKey = "vokasia";
    private const string MinioSecretKey = "vokasia_dev";
    private const string RedisConnection = "localhost:6379";

    private PostgreSqlContainer? _postgres;
    private RabbitMqContainer? _rabbitMq;
    private ServiceProvider? _workerServices;
    private IBusControl? _workerBus;
    private OutboxDispatcher? _outboxDispatcher;

    public async Task InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder().WithImage("postgres:17-alpine").Build();
        _rabbitMq = new RabbitMqBuilder().WithImage("rabbitmq:3-management-alpine").WithUsername("guest").WithPassword("guest").Build();
        await Task.WhenAll(_postgres.StartAsync(), _rabbitMq.StartAsync());

        // [BUG NYATA ditemukan+ditambal via suite ini sendiri, lihat DECISIONS.md D36] Migrasi HARUS
        // jalan LEWAT DbContext BERDIRI SENDIRI di sini, SEBELUM `this.Services` pertama kali
        // disentuh - menyentuh `Services` MEMICU WebApplicationFactory benar2 boot host (jalankan
        // top-level statement Program.cs SUNGGUHAN, termasuk `OpenIddictSetup.SeedOAuthClientsAsync`
        // di baris akhir Program.cs) - kalau skema belum ada saat itu (Postgres Testcontainers kosong
        // baru start), seeding OpenIddict gagal keras ("relation OpenIddictScopes does not exist").
        // Gap ini TAK PERNAH kelihatan di VokasiaApiFactory (Auth/, InMemory) krn EF InMemory tak
        // butuh skema/migrasi sama sekali - baru kelihatan begitu diuji thd Postgres SUNGGUHAN,
        // persis alasan ticket ini ada (Testcontainers, bukan InMemory).
        var migrationOptions = new DbContextOptionsBuilder<VokasiaDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options;
        // AmbientTenantContext kosong (tak pernah di-Set) - aman, migrasi skema tak pernah menyentuh
        // query berfilter tenant.
        await using (var migrationDb = new VokasiaDbContext(migrationOptions, new AmbientTenantContext()))
        {
            await migrationDb.Database.MigrateAsync();
        }

        await StartWorkerHostAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(BuildConnectionConfig());
        });
    }

    /// <summary>Config bersama Api (WebApplicationFactory) &amp; host Worker internal — SATU sumber, hindari drift string koneksi.</summary>
    private Dictionary<string, string?> BuildConnectionConfig() => new()
    {
        ["ConnectionStrings:Default"] = _postgres!.GetConnectionString(),
        ["RabbitMq:Host"] = _rabbitMq!.Hostname,
        ["RabbitMq:Port"] = _rabbitMq!.GetMappedPublicPort(5672).ToString(),
        ["RabbitMq:Username"] = "guest",
        ["RabbitMq:Password"] = "guest",
        ["Redis:Connection"] = RedisConnection,
        ["Minio:Endpoint"] = MinioEndpoint,
        ["Minio:AccessKey"] = MinioAccessKey,
        ["Minio:SecretKey"] = MinioSecretKey,
    };

    private async Task StartWorkerHostAsync()
    {
        // [BUG NYATA ditemukan+ditambal, lihat DECISIONS.md D36] QuestPDF mewajibkan deklarasi
        // lisensi SEKALI per proses SEBELUM Document.Create dipanggil manapun (Vokasia.Worker/
        // Program.cs baris paling atas melakukan ini utk proses Worker sungguhan) - proses xUnit
        // test TIDAK PERNAH menjalankan Program.cs Worker itu, jadi CertificateGeneratorConsumer
        // (memanggil CertificatePdfDocument.GeneratePdf() via QuestPDF) throw
        // "Please configure the QuestPDF license" tanpa baris ini. Ketahuan lewat kegagalan nyata
        // MassTransit S-FAULT, bukan diasumsikan dari baca kode semata.
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        var config = new ConfigurationBuilder().AddInMemoryCollection(BuildConnectionConfig()).Build();

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddDebug().AddConsole());
        // [BUG NYATA ditemukan+ditambal via suite CertificateFlowTests, lihat DECISIONS.md D36]
        // `Host.CreateApplicationBuilder`/`WebApplicationBuilder` PRODUKSI otomatis mendaftarkan
        // IConfiguration miliknya sendiri ke DI container - ServiceCollection MENTAH di sini TIDAK
        // (kita hanya memakai `config` sbg parameter biasa ke AddVokasiaInfrastructure/
        // AddVokasiaMassTransit, BUKAN meregistrasikannya sbg service) - consumer manapun yang
        // constructor-inject IConfiguration langsung (CertificateGeneratorConsumer: baca
        // "Minio:Bucket"/"Frontend:PublicUrl") gagal resolve tanpa baris ini. Ketahuan lewat
        // kegagalan nyata MassTransit ("Unable to resolve service for type IConfiguration"), bukan
        // diasumsikan dari baca kode semata.
        services.AddSingleton<IConfiguration>(config);
        // AddVokasiaInfrastructure = SATU titik yang sama dipakai Worker/Program.cs sungguhan:
        // DbContext(Npgsql)+Redis+MinIO+IdempotencyGuard+INotifier+IEmailSender. `env` Development
        // palsu (bukan null) supaya rantai IEmailSender jatuh ke DevLogEmailSender (log murni, TIDAK
        // pernah benar2 kirim SMTP keluar dari lingkungan test).
        services.AddVokasiaInfrastructure(config, new FakeDevelopmentEnvironment());

        // AssessmentCronJobs/JournalCronJobs: TANPA Hangfire di sini (cron "dipicu manual" via
        // TriggerOpenAssessmentPhaseAsync/TriggerEnqueueCertificateBatchAsync, AC ticket - lihat
        // doc-comment kelas) - cukup daftarkan kelasnya sbg scoped biasa, tak perlu scheduler.
        services.AddScoped<Vokasia.Worker.Jobs.AssessmentCronJobs>();
        services.AddScoped<Vokasia.Worker.Jobs.JournalCronJobs>();

        services.AddVokasiaMassTransit(config, x =>
        {
            x.AddConsumer<JournalSubmittedConsumer>();
            x.AddConsumer<StreakCounterConsumer>();
            x.AddConsumer<PhotoUploadedConsumer>();
            x.AddConsumer<JournalApprovedConsumer>();
            x.AddConsumer<JournalRejectedConsumer>();
            x.AddConsumer<MentorInvitedConsumer>();
            x.AddConsumer<PlacementCreatedConsumer>();
            x.AddConsumer<JournalReminderEmailConsumer>();
            x.AddConsumer<GhostingAlertEmailConsumer>();
            x.AddConsumer<ExportRequestedConsumer>();
            x.AddConsumer<CertificateGeneratorConsumer>();
        });

        _workerServices = services.BuildServiceProvider(validateScopes: true);
        _workerBus = _workerServices.GetRequiredService<IBusControl>();
        await _workerBus.StartAsync();

        // OutboxDispatcher: BackgroundService BIASA (bukan lewat generic Host) — StartAsync men-
        // trigger ExecuteAsync sbg Task latar belakang, pola umum uji BackgroundService tanpa host
        // penuh. Dipegang sbg field supaya DisposeAsync bisa StopAsync dgn rapi.
        _outboxDispatcher = new OutboxDispatcher(
            _workerServices.GetRequiredService<IServiceScopeFactory>(),
            _workerServices.GetRequiredService<ILogger<OutboxDispatcher>>());
        await _outboxDispatcher.StartAsync(CancellationToken.None);
    }

    public new async Task DisposeAsync()
    {
        if (_outboxDispatcher is not null) await _outboxDispatcher.StopAsync(CancellationToken.None);
        if (_workerBus is not null) await _workerBus.StopAsync();
        if (_workerServices is not null) await _workerServices.DisposeAsync();
        if (_rabbitMq is not null) await _rabbitMq.DisposeAsync();
        if (_postgres is not null) await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    /// <summary>Seed 1 user + dance code+PKCE PENUH (AuthTestHelpers, sama persis jalur Auth/) -&gt; HttpClient siap pakai dgn header Bearer terpasang. NFR-SEC-01: token SUNGGUHAN, bukan header palsu/test-auth-handler.</summary>
    public async Task<(AppUser User, HttpClient Client)> LoginAsAsync(UserRole role, Guid? tenantId, string emailPrefix)
    {
        var user = await AuthTestHelpers.SeedUserAsync(this, emailPrefix, role, tenantId);
        var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var (accessToken, _) = await AuthTestHelpers.LoginAndExchangeAsync(client, user.Email!);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return (user, client);
    }

    /// <summary>Baris Tenant minimal (FK dibutuhkan CertificateGeneratorConsumer/VerifyCertificate join db.Tenants) — bukan dummy GUID tanpa baris nyata spt fixture unit test lama.</summary>
    public async Task<Tenant> SeedTenantAsync(string schoolName = "SMK Uji Integrasi")
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
        var tenant = new Tenant { Id = Guid.NewGuid(), SchoolName = schoolName, IsActive = true };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        return tenant;
    }

    /// <summary>"Cron dipicu manual" (AC ticket) — resolve AssessmentCronJobs dari DI Worker (DB sama, TIDAK pakai AmbientTenantContext, konsisten pola produksi lintas-tenant).</summary>
    public async Task TriggerOpenAssessmentPhaseAsync(DateOnly runDate)
    {
        using var scope = _workerServices!.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<Vokasia.Worker.Jobs.AssessmentCronJobs>();
        await job.OpenAssessmentPhase(runDate);
    }

    public async Task TriggerEnqueueCertificateBatchAsync(DateOnly runDate)
    {
        using var scope = _workerServices!.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<Vokasia.Worker.Jobs.AssessmentCronJobs>();
        await job.EnqueueCertificateBatch(runDate);
    }

    /// <summary>Scope DbContext langsung ke Postgres Testcontainers — dipakai suite utk seed fixture/assert baris DB (pola sama SeedFixtureAsync di unit test lama, DbContext BUKAN InMemory di sini).</summary>
    public IServiceScope CreateDbScope() => Services.CreateScope();

    private sealed class FakeDevelopmentEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Vokasia.Tests.Integration";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

/// <summary>xUnit collection - Postgres+RabbitMQ Testcontainers per class fixture (bukan per-test) - 8 suite Integration/ berbagi SATU boot (mahal: 2 container + migrasi), TAPI dipaksa SEKUENSIAL (bukan paralel xUnit default) karena data lintas-tenant dibagi satu Postgres yang sama.</summary>
[CollectionDefinition("IntegrationTests")]
public class IntegrationTestCollection : ICollectionFixture<VokasiaIntegrationFactory>;
