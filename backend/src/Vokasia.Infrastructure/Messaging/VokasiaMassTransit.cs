using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Vokasia.Infrastructure.Messaging;

/// <summary>
/// VOK-H4-E1 §1 — AddVokasiaMassTransit. HANYA dipanggil dari Vokasia.Worker (bukan Vokasia.Api):
/// Api tidak pernah publish langsung ke broker (menulis OutboxMessage inline via EF Core biasa,
/// pola yang SUDAH ADA sejak H2-E1/H2-E3/H3-E1 - lihat OutboxEventContracts.cs) - hanya
/// OutboxDispatcher (Worker) yang benar-benar butuh IPublishEndpoint/IBus tersambung RabbitMQ, dan
/// hanya Worker yang meng-host consumer (menerima pesan). Menghindari koneksi RabbitMQ yang tak
/// perlu dari proses Api.
///
/// configureConsumers: callback registrasi consumer (x.AddConsumer&lt;T&gt;() dst.) - disuntik dari
/// Worker/Program.cs (Infrastructure tidak bisa reference tipe Consumer yang hidup di assembly
/// Worker, arah dependency proyek ini: Worker -> Infrastructure, bukan sebaliknya).
/// </summary>
public static class VokasiaMassTransit
{
    public static IServiceCollection AddVokasiaMassTransit(
        this IServiceCollection services,
        IConfiguration config,
        Action<IBusRegistrationConfigurator>? configureConsumers = null)
    {
        services.AddMassTransit(x =>
        {
            configureConsumers?.Invoke(x);

            x.UsingRabbitMq((context, cfg) =>
            {
                var host = config["RabbitMq:Host"] ?? "localhost";
                var username = config["RabbitMq:Username"] ?? "vokasia";
                var password = config["RabbitMq:Password"] ?? "vokasia_dev";
                // VOK-H4-E3 §1: port opsional - dev/prod TAK PERNAH set kunci ini (fallback 5672,
                // NOL perubahan perilaku) - hanya suite Async/ (Testcontainers.RabbitMq, port host
                // di-mapping RANDOM demi hindari bentrok) yang mengisinya. Overload host+port
                // (bukan cuma host) dipilih drpd bikin config MassTransit terpisah utk test, supaya
                // suite Async/ menguji RIWAYAT CONFIG PRODUKSI yang SAMA PERSIS (MessagingDefaults
                // dst di bawah), bukan tiruan yang bisa diam2 menyimpang dari yang sungguhan dipakai.
                var port = ushort.TryParse(config["RabbitMq:Port"], out var pt) ? pt : (ushort)5672;

                cfg.Host(host, port, "/", h =>
                {
                    h.Username(username);
                    h.Password(password);
                });

                // Retry cepat (in-memory, tanpa requeue) SEBELUM redelivery (requeue via delay
                // exchange, utk kegagalan yg butuh waktu lebih - broker restart, dependency
                // downstream hiccup dst.). Kalau KEDUANYA habis, MassTransit otomatis pindah pesan
                // ke "{queue}_error" (DLQ per queue, AC ticket) - bawaan transport RabbitMQ, tanpa
                // config tambahan. VOK-H4-E3 §3: angka dipindah ke MessagingDefaults (satu sumber,
                // TIDAK diubah nilainya dari H4-E1 - lihat doc-comment kelas itu).
                cfg.UseMessageRetry(r => r.Exponential(
                    retryLimit: MessagingDefaults.RetryLimit,
                    minInterval: MessagingDefaults.RetryMinInterval,
                    maxInterval: MessagingDefaults.RetryMaxInterval,
                    intervalDelta: MessagingDefaults.RetryIntervalDelta));
                cfg.UseDelayedRedelivery(r => r.Intervals(MessagingDefaults.RedeliveryIntervals));

                // Prefetch wajar (AC ticket) - app skala ratusan siswa/tenant, bukan jutaan pesan/dtk;
                // MessagingDefaults.PrefetchCount cukup utk paralelisme berguna tanpa consumer
                // kebanjiran batch besar sekaligus.
                cfg.PrefetchCount = MessagingDefaults.PrefetchCount;

                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
