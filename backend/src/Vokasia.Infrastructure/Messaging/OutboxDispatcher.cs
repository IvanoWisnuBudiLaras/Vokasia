using System.Text.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vokasia.Domain.Events;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Infrastructure.Messaging;

/// <summary>
/// VOK-H4-E1 §1 OutboxDispatcher — poll batch 50 tiap 2 dtk, publish ke RabbitMQ via MassTransit,
/// tandai PublishedAt. Gagal (mis. broker mati) -> baris TETAP unpublished (retry alami poll
/// berikutnya, tanpa penanganan error eksplisit selain log+berhenti siklus ini).
///
/// Urutan per aggregate terjaga (AC ticket) via: query ORDER BY OccurredAt (bukan random/PK), dan
/// "berhenti di kegagalan pertama" per siklus poll (BUKAN skip-lanjut) - kalau pesan #1 gagal
/// publish (broker turun), pesan #2..50 TIDAK dicoba di siklus ini juga, supaya tak ada pesan yang
/// "melompati" pesan lain yang lebih tua tapi masih tertahan gagal. Trade-off disadari: 1 pesan
/// bermasalah bisa menahan SELURUH antrean sampai teratasi - mitigasi: tipe tak dikenal (lihat
/// TypeRegistry) TIDAK dianggap "gagal, coba lagi" (akan macet permanen, retry tak menolong bug
/// kode) - ditandai published+log error keras supaya operator sadar, bukan diam-diam menumpuk.
///
/// [KRUSIAL utk idempotency, AC "message sama 2x -> efek 1x"]: MessageId MassTransit di-set
/// EKSPLISIT = OutboxMessage.Id (BUKAN dibiarkan auto-generate NewId() per panggilan Publish) -
/// kalau baris outbox yang SAMA sempat dipublish 2x (mis. proses ini crash tepat setelah publish
/// sukses tapi SEBELUM SaveChangesAsync menandai PublishedAt, sehingga siklus berikutnya
/// mempublish ulang baris yang SAMA), kedua publish itu akan membawa MessageId yang SAMA PERSIS -
/// IdempotencyGuard (keyed ConsumerName+MessageId) di consumer bisa mendeteksi & menolak duplikat
/// itu. Tanpa ini, tiap Publish() otomatis dapat MessageId BARU tiap panggilan (nol jaminan
/// idempotency dari sisi dispatcher, seluruh beban dedup jatuh ke asumsi "delivery MassTransit
/// sendiri selalu tepat 1x" yang TIDAK benar utk skenario spesifik ini).
/// </summary>
public class OutboxDispatcher(IServiceScopeFactory scopeFactory, ILogger<OutboxDispatcher> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private const int BatchSize = 50;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Peta Type string (OutboxMessage.Type) -> tipe CLR kontrak (Vokasia.Domain.Events).</summary>
    private static readonly IReadOnlyDictionary<string, Type> TypeRegistry = new Dictionary<string, Type>
    {
        ["JournalSubmitted"] = typeof(JournalSubmittedEvent),
        ["PhotoUploaded"] = typeof(PhotoUploadedEvent),
        ["JournalApproved"] = typeof(JournalApprovedEvent),
        ["JournalRejected"] = typeof(JournalRejectedEvent),
        ["MentorInvited"] = typeof(MentorInvitedEvent),
        ["PlacementCreated"] = typeof(PlacementCreatedEvent),
        ["JournalReminderEmailRequested"] = typeof(JournalReminderEmailRequestedEvent),
        // VOK-H4-E3: GAP ditutup - sebelum baris ini, Type ini SELALU "tak dikenal" (lihat doc-comment
        // GhostingAlertEmailRequestedEvent, OutboxEventContracts.cs).
        ["GhostingAlertEmailRequested"] = typeof(GhostingAlertEmailRequestedEvent),
        // VOK-H5-E1: didaftarkan SEJAK AWAL (bukan ditambal belakangan spt GhostingAlert) - lihat
        // doc-comment masing-masing event, OutboxEventContracts.cs.
        ["AssessmentFinalized"] = typeof(AssessmentFinalizedEvent),
        ["ExportRequested"] = typeof(ExportRequestedEvent),
        ["CertificateRequested"] = typeof(CertificateRequestedEvent),
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        do
        {
            try
            {
                await DispatchOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Kegagalan tak terduga di LUAR try/catch per-pesan DispatchOnceAsync (mis. DB itu
                // sendiri tak terjangkau) - jangan sampai BackgroundService mati total, cukup log &
                // coba lagi siklus berikutnya (retry alami, sama semangatnya dgn kegagalan publish).
                logger.LogError(ex, "OutboxDispatcher: siklus poll gagal tak terduga, dicoba lagi 2 dtk lagi.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task DispatchOnceAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        var batch = await db.OutboxMessages
            .Where(m => m.PublishedAt == null)
            .OrderBy(m => m.OccurredAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (batch.Count == 0)
        {
            return;
        }

        var publishedCount = 0;
        foreach (var message in batch)
        {
            if (!TypeRegistry.TryGetValue(message.Type, out var clrType))
            {
                // Tipe tak dikenal = bug kode (Type string typo, atau consumer/kontrak baru belum
                // didaftarkan di TypeRegistry) - BUKAN kegagalan transient. Retry tak akan pernah
                // menolongnya (tipe tetap tak dikenal selamanya tanpa deploy baru) - ditandai
                // published (hindari macet permanen menahan antrean) + log ERROR keras.
                logger.LogError(
                    "OutboxDispatcher: OutboxMessage {Id} bertipe {Type} tak dikenal (tak terdaftar di TypeRegistry) - ditandai published TANPA dipublish, cek kode.",
                    message.Id, message.Type);
                message.PublishedAt = DateTimeOffset.UtcNow;
                publishedCount++;
                continue;
            }

            object? payload;
            try
            {
                payload = JsonSerializer.Deserialize(message.PayloadJson, clrType, JsonOptions);
            }
            catch (JsonException ex)
            {
                logger.LogError(ex,
                    "OutboxDispatcher: OutboxMessage {Id} bertipe {Type} gagal deserialize payload - ditandai published TANPA dipublish, cek kode/data.",
                    message.Id, message.Type);
                message.PublishedAt = DateTimeOffset.UtcNow;
                publishedCount++;
                continue;
            }

            if (payload is null)
            {
                logger.LogError("OutboxDispatcher: OutboxMessage {Id} bertipe {Type} deserialize jadi null - dilewati.", message.Id, message.Type);
                message.PublishedAt = DateTimeOffset.UtcNow;
                publishedCount++;
                continue;
            }

            try
            {
                await publishEndpoint.Publish(payload, clrType, context => context.MessageId = message.Id, ct);
                message.PublishedAt = DateTimeOffset.UtcNow;
                publishedCount++;
            }
            catch (Exception ex)
            {
                // Kegagalan publish SUNGGUHAN (broker mati dst.) - BERHENTI di sini (bukan lanjut ke
                // pesan berikutnya), jaga urutan OccurredAt (lihat doc-comment kelas). Pesan ini &
                // sisanya tetap unpublished, dicoba lagi siklus poll berikutnya (2 dtk).
                logger.LogWarning(ex,
                    "OutboxDispatcher: publish OutboxMessage {Id} ({Type}) gagal - berhenti siklus ini, {Remaining} pesan tersisa akan dicoba lagi siklus berikutnya.",
                    message.Id, message.Type, batch.Count - publishedCount);
                break;
            }
        }

        if (publishedCount > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("OutboxDispatcher: {Count}/{Batch} pesan berhasil dipublish siklus ini.", publishedCount, batch.Count);
        }
    }
}
