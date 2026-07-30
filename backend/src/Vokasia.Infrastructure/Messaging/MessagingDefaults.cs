namespace Vokasia.Infrastructure.Messaging;

/// <summary>
/// Satu sumber untuk retry 5x eksponensial 1-30 detik dan prefetch 16 yang dipakai
/// <see cref="VokasiaMassTransit"/>.
///
/// Nama antrean `_error` (DLQ per queue) SENGAJA TIDAK punya konstanta di sini — itu bukan nilai
/// yang KAMI atur, melainkan konvensi bawaan MassTransit sendiri (RabbitMqReceiveEndpointExtensions):
/// tiap queue "X" otomatis dapat pasangan "X_error" saat retry habis, dibuat otomatis
/// oleh transport, tanpa config eksplisit apa pun sisi aplikasi. Didokumentasikan di sini sbg
/// PENGETAHUAN (dipakai test Async/ & tools/Replay-Dlq.ps1 utk tahu nama queue DLQ), bukan dikontrol.
/// </summary>
public static class MessagingDefaults
{
    /// <summary>Percobaan retry cepat in-memory tanpa requeue.</summary>
    public const int RetryLimit = 5;

    public static readonly TimeSpan RetryMinInterval = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan RetryMaxInterval = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan RetryIntervalDelta = TimeSpan.FromSeconds(5);

    /// <summary>Paralelisme per consumer instance — app skala ratusan siswa/tenant, bukan jutaan
    /// pesan/dtk; nilai wajar tanpa consumer kebanjiran batch besar sekaligus.</summary>
    public const int PrefetchCount = 16;

    /// <summary>Suffix DLQ bawaan MassTransit/RabbitMQ — lihat doc-comment kelas.</summary>
    public const string DeadLetterQueueSuffix = "_error";

    public static string DeadLetterQueueNameFor(string queueName) => queueName + DeadLetterQueueSuffix;
}
