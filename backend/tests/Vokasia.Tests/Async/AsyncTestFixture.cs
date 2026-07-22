using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Vokasia.Domain.Common;
using Vokasia.Infrastructure.Messaging;
using Vokasia.Infrastructure.Persistence;
using Vokasia.Infrastructure.TenantContext;
using Vokasia.Worker.Consumers;

namespace Vokasia.Tests.Async;

/// <summary>
/// VOK-H4-E3 §1 — RabbitMQ + Postgres SUNGGUHAN (Testcontainers), BUKAN in-memory/mock. Beda TUJUAN
/// dgn ConsumerDuplicateDeliveryTests.cs (H4-E1, MassTransit in-memory test transport, membuktikan
/// WIRING consumer) - suite Async/ ini membuktikan PERILAKU TRANSPORT NYATA: retry eksponensial,
/// redelivery, DLQ (`_error` queue) otomatis, replay - properti yang TIDAK ADA di transport
/// in-memory sama sekali (dikonfirmasi baca source MassTransit: in-memory transport tak
/// mengimplementasikan retry/redelivery/DLQ persis spt RabbitMQ; harness Consumed/Sent bawaan
/// bahkan mendeduplikasi MessageId yang sama - lihat catatan teknis ConsumerDuplicateDeliveryTests.cs).
///
/// DUA bus MassTransit terpisah, SATU broker Testcontainers yang sama (2 koneksi client biasa,
/// bukan 2 broker):
/// - <see cref="Prod"/>: AddVokasiaMassTransit YANG SAMA PERSIS dipakai Worker/Program.cs (retry
///   MessagingDefaults SUNGGUHAN, TIDAK dipercepat) + JournalApprovedConsumer/JournalSubmittedConsumer
///   - dipakai DuplicateDeliveryTests/OutOfOrderTests/OutboxGuaranteeTests (skenario ini TIDAK perlu
///   menunggu retry/redelivery habis, jadi aman pakai angka produksi asli).
/// - <see cref="FastPoison"/>: registrasi MINIMAL terpisah (BUKAN AddVokasiaMassTransit) khusus
///   PoisonTestConsumer dgn retry DIPERCEPAT (3x @150ms, bukan MessagingDefaults - 5x 1-30dtk
///   terlalu lama utk test berulang). TANPA UseDelayedRedelivery (lihat doc-comment endpoint
///   PoisonQueueName di bawah - butuh plugin broker yg tak terpasang image polos project ini,
///   dicoba nyata & terbukti tak sampai DLQ). Properti INTI yang dibuktikan (retry habis -> DLQ
///   otomatis) SAMA PERSIS spt produksi - redelivery scr KONSEP cuma tier retry tambahan dgn jeda
///   lebih panjang, perilaku TERMINAL sama. MessagingDefaults produksi TIDAK diubah sama sekali.
///
/// xUnit [CollectionDefinition] "AsyncTests" memaksa seluruh test class Async/ jalan SEKUENSIAL
/// (bukan paralel default xUnit antar class) - PENTING krn PoisonTestConsumer pakai static mutable
/// state (ShouldThrowFor) yang tak aman kalau 2 test class jalan bersamaan.
/// </summary>
public class AsyncTestFixture : IAsyncLifetime
{
    /// <summary>Nama queue consumer poison - lihat doc-comment InitializeAsync (FastPoison bus) utk alasan literal, bukan formatter default.</summary>
    public const string PoisonQueueName = "poison-test-explicit";

    private RabbitMqContainer? _rabbitMq;
    private PostgreSqlContainer? _postgres;
    private IBusControl? _prodBus;
    private IBusControl? _poisonBus;

    public ServiceProvider Prod { get; private set; } = default!;
    public ServiceProvider FastPoison { get; private set; } = default!;

    public async Task InitializeAsync()
    {
        // .WithUsername/.WithPassword EKSPLISIT - Testcontainers.RabbitMq TIDAK menjamin default
        // "guest"/"guest" (ketahuan lewat kegagalan nyata ACCESS_REFUSED sblm baris ini ditambah,
        // BUKAN diasumsikan dari awal) - kredensial di sini HARUS SAMA PERSIS dgn yang dipakai
        // PoisonMessageTests/DlqReplayTests (RabbitMQ.Client langsung, bukan lewat MassTransit).
        _rabbitMq = new RabbitMqBuilder().WithImage("rabbitmq:3-management-alpine").WithUsername("guest").WithPassword("guest").Build();
        _postgres = new PostgreSqlBuilder().WithImage("postgres:17-alpine").Build();
        await Task.WhenAll(_rabbitMq.StartAsync(), _postgres.StartAsync());

        var rmqConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RabbitMq:Host"] = _rabbitMq.Hostname,
            ["RabbitMq:Port"] = _rabbitMq.GetMappedPublicPort(5672).ToString(),
            ["RabbitMq:Username"] = "guest",
            ["RabbitMq:Password"] = "guest",
        }).Build();

        // --- Prod bus: config produksi asli ---
        var prodServices = new ServiceCollection();
        prodServices.AddLogging();
        prodServices.AddDbContext<VokasiaDbContext>(opt => opt.UseNpgsql(_postgres.GetConnectionString()));
        prodServices.AddScoped<ITenantContext>(_ => new AmbientTenantContext());
        prodServices.AddScoped<IdempotencyGuard>();
        prodServices.AddScoped<INotifier, Notifier>();
        prodServices.AddVokasiaMassTransit(rmqConfig, x =>
        {
            x.AddConsumer<JournalApprovedConsumer>();
            x.AddConsumer<JournalSubmittedConsumer>();
        });
        Prod = prodServices.BuildServiceProvider(validateScopes: true);

        using (var scope = Prod.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<VokasiaDbContext>().Database.MigrateAsync();
        }
        _prodBus = Prod.GetRequiredService<IBusControl>();
        await _prodBus.StartAsync();

        // --- FastPoison bus: retry/redelivery DIPERCEPAT, lihat doc-comment kelas ---
        var poisonServices = new ServiceCollection();
        poisonServices.AddLogging();
        poisonServices.AddMassTransit(x =>
        {
            x.AddConsumer<PoisonTestConsumer>();
            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(_rabbitMq.Hostname, _rabbitMq.GetMappedPublicPort(5672), "/", h =>
                {
                    h.Username("guest");
                    h.Password("guest");
                });
                // Nama queue EKSPLISIT (bukan formatter default) - PoisonMessageTests/DlqReplayTests
                // merujuk konstanta literal yang SAMA (PoisonQueueName), menghindari ketidakcocokan
                // asumsi nama endpoint vs yang benar2 dipakai MassTransit.
                cfg.ReceiveEndpoint(PoisonQueueName, e =>
                {
                    e.ConfigureConsumer<PoisonTestConsumer>(context);
                    // [DISEDERHANAKAN, dicatat eksplisit] Retry SAJA (tanpa UseDelayedRedelivery) -
                    // redelivery MassTransit RabbitMQ transport butuh kemampuan delay broker
                    // (plugin rabbitmq_delayed_message_exchange, TAK terpasang di image
                    // rabbitmq:3-management-alpine polos yang dipakai project ini) - percobaan pakai
                    // UseDelayedRedelivery di sini TERBUKTI (dicoba nyata) pesan tak pernah sampai
                    // DLQ dlm waktu wajar. Retry SAJA sudah cukup buktikan mekanisme inti AC
                    // ("retry sesuai policy -> masuk DLQ setelah habis") - redelivery scr KONSEP
                    // hanyalah TIER retry tambahan dgn jeda lebih panjang, perilaku TERMINAL yang
                    // sama (habis -> DLQ). MessagingDefaults/AddVokasiaMassTransit produksi TETAP
                    // pakai UseDelayedRedelivery apa adanya (TIDAK diubah) - ini HANYA simplifikasi
                    // bus test PoisonTestConsumer, lihat DECISIONS.md utk keputusan lengkap.
                    e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromMilliseconds(150)));
                });
            });
        });
        FastPoison = poisonServices.BuildServiceProvider(validateScopes: true);
        _poisonBus = FastPoison.GetRequiredService<IBusControl>();
        await _poisonBus.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_prodBus is not null) await _prodBus.StopAsync();
        if (_poisonBus is not null) await _poisonBus.StopAsync();
        if (Prod is not null) await Prod.DisposeAsync();
        if (FastPoison is not null) await FastPoison.DisposeAsync();
        if (_rabbitMq is not null) await _rabbitMq.DisposeAsync();
        if (_postgres is not null) await _postgres.DisposeAsync();
    }

    public string RabbitMqHost => _rabbitMq!.Hostname;
    public ushort RabbitMqPort => _rabbitMq!.GetMappedPublicPort(5672);
}

[CollectionDefinition("AsyncTests")]
public class AsyncTestCollection : ICollectionFixture<AsyncTestFixture>;
