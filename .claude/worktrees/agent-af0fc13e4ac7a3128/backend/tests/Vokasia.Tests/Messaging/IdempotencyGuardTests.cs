using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vokasia.Infrastructure.Messaging;
using Vokasia.Infrastructure.Persistence;
using Vokasia.Tests.Auth;

namespace Vokasia.Tests.Messaging;

/// <summary>
/// AC VOK-H4-E1 §3 (idempotency consumer: "message sama dikirim 2x -> efek 1x"). Menguji
/// IdempotencyGuard LANGSUNG (bukan lewat consumer/bus) - properti intinya murni soal baris
/// ProcessedMessage(ConsumerName,MessageId), independen dari consumer mana pun yang memanggilnya.
/// Reuse VokasiaApiFactory HANYA utk scope+DbContext InMemory yang konsisten dgn suite lain (sama
/// alasan JournalCronJobsTests) - tak butuh web host sungguhan.
/// </summary>
public class IdempotencyGuardTests : IClassFixture<VokasiaApiFactory>
{
    private readonly VokasiaApiFactory _factory;
    public IdempotencyGuardTests(VokasiaApiFactory factory) => _factory = factory;

    [Fact]
    public async Task EnsureNotProcessedAsync_FirstCall_ReturnsTrueAndPersistsMarker()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
        var guard = new IdempotencyGuard(db);
        var messageId = Guid.NewGuid();

        var result = await guard.EnsureNotProcessedAsync("TestConsumerA", messageId, default);
        await db.SaveChangesAsync();

        Assert.True(result);
        Assert.Single(db.ProcessedMessages.Where(p => p.ConsumerName == "TestConsumerA" && p.MessageId == messageId));
    }

    /// <summary>Skenario AC persis: "message sama dikirim 2x" — redelivery SEKUENSIAL (commit
    /// pertama SELESAI sebelum cek kedua dimulai), lihat doc-comment IdempotencyGuard.cs soal
    /// batas desain thd true-concurrent race.</summary>
    [Fact]
    public async Task EnsureNotProcessedAsync_SameMessageRedeliveredAfterCommit_SecondCallReturnsFalse()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
        var guard = new IdempotencyGuard(db);
        var messageId = Guid.NewGuid();

        var first = await guard.EnsureNotProcessedAsync("TestConsumerB", messageId, default);
        await db.SaveChangesAsync();

        var second = await guard.EnsureNotProcessedAsync("TestConsumerB", messageId, default);
        await db.SaveChangesAsync();

        Assert.True(first);
        Assert.False(second);
        // TIDAK ada baris duplikat - kedua cek berujung pada SATU baris (PK ConsumerName+MessageId).
        Assert.Single(db.ProcessedMessages.Where(p => p.ConsumerName == "TestConsumerB" && p.MessageId == messageId));
    }

    /// <summary>Guard ditambahkan ke ChangeTracker TANPA SaveChanges sendiri (lihat doc-comment) -
    /// kalau caller GAGAL/tak pernah SaveChanges, penanda TIDAK benar2 tersimpan -> redelivery
    /// berikutnya dianggap BELUM diproses (rollback implisit, konsisten dgn efek bisnis yg juga
    /// batal). Ini MEMBUKTIKAN bagian "atomik dgn efek bisnis" dari desainnya, bukan cuma
    /// mengasumsikan.</summary>
    [Fact]
    public async Task EnsureNotProcessedAsync_CallerNeverSaves_MarkerNotPersisted_NextCallStillReturnsTrue()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
        var guard = new IdempotencyGuard(db);
        var messageId = Guid.NewGuid();

        var first = await guard.EnsureNotProcessedAsync("TestConsumerC", messageId, default);
        // SENGAJA tak panggil SaveChangesAsync - simulasi consumer yg throw SEBELUM commit sendiri.
        Assert.True(first);

        using var freshScope = _factory.Services.CreateScope();
        var freshDb = freshScope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
        var freshGuard = new IdempotencyGuard(freshDb);
        var second = await freshGuard.EnsureNotProcessedAsync("TestConsumerC", messageId, default);

        Assert.True(second); // penanda pertama tak pernah commit -> retry dianggap belum diproses.
    }

    [Fact]
    public async Task EnsureNotProcessedAsync_SameMessageIdDifferentConsumerName_BothReturnTrue()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
        var guard = new IdempotencyGuard(db);
        var messageId = Guid.NewGuid();

        var a = await guard.EnsureNotProcessedAsync("ConsumerA", messageId, default);
        var b = await guard.EnsureNotProcessedAsync("ConsumerB", messageId, default);
        await db.SaveChangesAsync();

        // PK gabungan (ConsumerName,MessageId) - dua consumer BERBEDA yang bereaksi ke event/pesan
        // fisik yang SAMA (persis kasus JournalSubmittedConsumer + StreakCounterConsumer) masing2
        // dedupe SENDIRI-SENDIRI, tak saling memblokir.
        Assert.True(a);
        Assert.True(b);
        Assert.Equal(2, db.ProcessedMessages.Count(p => p.MessageId == messageId));
    }
}
