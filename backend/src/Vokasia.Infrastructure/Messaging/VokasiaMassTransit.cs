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

                cfg.Host(host, "/", h =>
                {
                    h.Username(username);
                    h.Password(password);
                });

                // Retry cepat (in-memory, tanpa requeue) 5x exponential 1s->30s (AC ticket) SEBELUM
                // redelivery (requeue via delay exchange, utk kegagalan yg butuh waktu lebih -
                // broker restart, dependency downstream hiccup dst.). Kalau KEDUANYA habis, MassTransit
                // otomatis pindah pesan ke "{queue}_error" (DLQ per queue, AC ticket) - bawaan
                // transport RabbitMQ, tanpa config tambahan.
                cfg.UseMessageRetry(r => r.Exponential(
                    retryLimit: 5,
                    minInterval: TimeSpan.FromSeconds(1),
                    maxInterval: TimeSpan.FromSeconds(30),
                    intervalDelta: TimeSpan.FromSeconds(5)));
                cfg.UseDelayedRedelivery(r => r.Intervals(
                    TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5)));

                // Prefetch wajar (AC ticket) - app skala ratusan siswa/tenant, bukan jutaan pesan/dtk;
                // 16 cukup utk paralelisme berguna tanpa consumer kebanjiran batch besar sekaligus.
                cfg.PrefetchCount = 16;

                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
