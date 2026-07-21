namespace Vokasia.Domain.Entities;

/// <summary>
/// Transactional outbox (FR-X-02). Ditulis DALAM transaksi yang sama dengan perubahan data oleh
/// SaveToOutboxInterceptor (Infrastructure); dipublish OutboxDispatcher (Worker, H4-E1).
/// Broker RabbitMQ down != event hilang — inilah jaminannya.
/// </summary>
public class OutboxMessage
{
    public Guid Id { get; set; }
    public string Type { get; set; } = default!;
    public string PayloadJson { get; set; } = default!;
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PublishedAt { get; set; }
}

/// <summary>
/// Penanda idempotency consumer (H4-E1). PK gabungan (ConsumerName, MessageId) dikonfigurasi di
/// DbContext — duplicate delivery discek di sini sebelum efek samping dijalankan.
/// </summary>
public class ProcessedMessage
{
    public string ConsumerName { get; set; } = default!;
    public Guid MessageId { get; set; }
    public DateTimeOffset ProcessedAt { get; set; } = DateTimeOffset.UtcNow;
}
