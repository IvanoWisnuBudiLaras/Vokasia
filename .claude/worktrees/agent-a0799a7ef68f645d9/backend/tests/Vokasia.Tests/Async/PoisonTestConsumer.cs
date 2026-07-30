using System.Collections.Concurrent;
using MassTransit;

namespace Vokasia.Tests.Async;

/// <summary>
/// VOK-H4-E3 §1 PoisonMessageTests/DlqReplayTests — consumer SINTETIK khusus test (bukan consumer
/// produksi apa pun). "Poison message -> retry sesuai policy -> masuk DLQ" adalah properti TRANSPORT
/// (MassTransit+RabbitMQ), SAMA untuk consumer produksi manapun (semua pakai UseMessageRetry +
/// UseDelayedRedelivery yang SAMA, MessagingDefaults) - satu consumer yang throw TERKENDALI (via
/// <see cref="ShouldThrowFor"/>, keyed per Guid pesan supaya aman antar test meski jalan sekuensial
/// dlm 1 collection) sudah cukup MEMBUKTIKAN mekanismenya tanpa perlu 4 consumer produksi berbeda
/// masing2 dipaksa gagal (yang hanya mengulang bukti yang SAMA 4x dgn ongkos setup domain jauh
/// lebih besar per consumer produksi, tanpa menambah keyakinan apa pun soal transport itu sendiri).
/// </summary>
public class PoisonTestEvent
{
    public Guid Id { get; set; }
}

public class PoisonTestConsumer : IConsumer<PoisonTestEvent>
{
    /// <summary>Id pesan -> true berarti SELALU throw (poison permanen, utk PoisonMessageTests).
    /// DlqReplayTests men-set FALSE utk Id yg sama SEBELUM replay - mensimulasikan "penyebab
    /// kegagalan sudah diperbaiki", membuktikan replay benar2 berujung SUKSES, bukan cuma
    /// "pesan dipindah balik ke antrean asal" tanpa bukti ia bisa diproses.</summary>
    public static readonly ConcurrentDictionary<Guid, bool> ShouldThrowFor = new();

    /// <summary>Id pesan yang PERNAH berhasil diproses (utk asersi DlqReplayTests).</summary>
    public static readonly ConcurrentBag<Guid> Processed = new();

    public Task Consume(ConsumeContext<PoisonTestEvent> context)
    {
        var id = context.Message.Id;
        if (ShouldThrowFor.TryGetValue(id, out var shouldThrow) && shouldThrow)
        {
            throw new InvalidOperationException($"[PoisonTestConsumer] sengaja gagal permanen utk pesan {id} (simulasi poison message).");
        }

        Processed.Add(id);
        return Task.CompletedTask;
    }
}
