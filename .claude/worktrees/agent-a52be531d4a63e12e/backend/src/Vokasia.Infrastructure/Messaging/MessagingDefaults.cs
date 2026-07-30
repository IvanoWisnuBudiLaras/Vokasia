namespace Vokasia.Infrastructure.Messaging;

/// <summary>
/// VOK-H4-E3 §3 "Review policy H4-E1 → satu sumber konstanta" — nilai di bawah CERMIN PERSIS apa
/// yang sudah dipasang <see cref="VokasiaMassTransit"/> sejak H4-E1 (retry 5x eksponensial 1-30dtk,
/// redelivery 30dtk/1mnt/5mnt, prefetch 16) — DIPINDAHKAN ke sini, BUKAN diubah nilainya (H4-E1
/// sudah lolos test+live-verification broker-down/recovery dgn angka ini; mengubah nilai skrg tanpa
/// alasan baru adalah risiko regresi tanpa manfaat). Tujuan AC ticket ("terdokumentasi di satu
/// file") murni konsolidasi, bukan tuning ulang.
///
/// Nama antrean `_error` (DLQ per queue) SENGAJA TIDAK punya konstanta di sini — itu bukan nilai
/// yang KAMI atur, melainkan konvensi bawaan MassTransit sendiri (RabbitMqReceiveEndpointExtensions):
/// tiap queue "X" otomatis dapat pasangan "X_error" saat retry+redelivery habis, dibuat otomatis
/// oleh transport, tanpa config eksplisit apa pun sisi aplikasi. Didokumentasikan di sini sbg
/// PENGETAHUAN (dipakai test Async/ & tools/Replay-Dlq.ps1 utk tahu nama queue DLQ), bukan dikontrol.
/// </summary>
public static class MessagingDefaults
{
    /// <summary>Percobaan retry cepat in-memory (tanpa requeue), SEBELUM redelivery.</summary>
    public const int RetryLimit = 5;

    public static readonly TimeSpan RetryMinInterval = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan RetryMaxInterval = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan RetryIntervalDelta = TimeSpan.FromSeconds(5);

    /// <summary>Redelivery (requeue via delay-exchange) SETELAH retry cepat di atas habis — utk
    /// kegagalan yg butuh waktu lebih (broker restart, dependency downstream hiccup dst.). Habis
    /// juga -> MassTransit pindahkan pesan ke "{queue}_error" (DLQ) otomatis.</summary>
    public static readonly TimeSpan[] RedeliveryIntervals =
    [
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
    ];

    /// <summary>Paralelisme per consumer instance — app skala ratusan siswa/tenant, bukan jutaan
    /// pesan/dtk; nilai wajar tanpa consumer kebanjiran batch besar sekaligus.</summary>
    public const int PrefetchCount = 16;

    /// <summary>Suffix DLQ bawaan MassTransit/RabbitMQ — lihat doc-comment kelas.</summary>
    public const string DeadLetterQueueSuffix = "_error";

    public static string DeadLetterQueueNameFor(string queueName) => queueName + DeadLetterQueueSuffix;
}
