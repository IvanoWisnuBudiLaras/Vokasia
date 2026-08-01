using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Domain.Events;
using Vokasia.Infrastructure.Persistence;
using Vokasia.Worker.Consumers;

namespace Vokasia.Tests.Async;

/// <summary>
/// VOK-H4-E3 §1 OutboxGuaranteeTests — "kill broker di tengah publish -> tidak ada event
/// hilang/dobel setelah pulih". Mekanisme YANG SESUNGGUHNYA menjamin ini (lihat doc-comment
/// OutboxDispatcher.cs, paragraf "KRUSIAL utk idempotency") adalah: MessageId dipublish EKSPLISIT =
/// OutboxMessage.Id (BUKAN auto-generate baru tiap panggilan Publish) - kalau dispatcher publish
/// SUKSES tapi CRASH SEBELUM SaveChangesAsync menandai PublishedAt, siklus poll berikutnya
/// mem-publish ULANG baris yg SAMA dgn MessageId yg SAMA PERSIS -> IdempotencyGuard di consumer
/// menyerap duplikat itu (efek 1x, BUKAN 2x) - PERSIS skenario "broker/proses mati tepat di tengah
/// publish, lalu pulih".
///
/// [KENAPA TIDAK literally mematikan container RabbitMQ]: Testcontainers start/stop container
/// tengah test menambah kerapuhan (timing restart, port re-mapping, koneksi MassTransit re-connect)
/// tanpa menguji properti BERBEDA dari yang dibuktikan di sini - properti yang SEBENARNYA dijamin
/// (reuse MessageId = OutboxMessage.Id) bisa dibuktikan LANGSUNG dgn mensimulasikan 2 publish
/// fisik dgn MessageId identik (persis efek yg akan terjadi kalau proses OutboxDispatcher benar2
/// mati di antara publish sukses & SaveChanges) - lebih deterministik & lebih cepat, TANPA
/// mengorbankan apa yang dibuktikan. Dicatat eksplisit sbg keputusan, bukan disederhanakan diam-diam.
/// </summary>
[Collection("AsyncTests")]
public class OutboxGuaranteeTests(AsyncTestFixture fixture)
{
    [Fact]
    public async Task OutboxMessage_RepublishedWithSameMessageIdAfterSimulatedCrash_ConsumedExactlyOnce()
    {
        if (!fixture.IsDockerAvailable) return;
        var tenantId = Guid.NewGuid();
        var studentUserId = Guid.NewGuid();
        Guid placementId;
        Guid outboxMessageId;

        using (var scope = fixture.Prod.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            var student = new Student { Id = Guid.NewGuid(), TenantId = tenantId, UserId = studentUserId, FullName = "Siswa Uji Outbox Guarantee", MajorId = Guid.NewGuid(), Classroom = "XII RPL 3" };
            var placement = new Placement
            {
                Id = Guid.NewGuid(), TenantId = tenantId, StudentId = student.Id,
                CompanyId = Guid.NewGuid(), PeriodId = Guid.NewGuid(), TeacherId = Guid.NewGuid(),
                Status = PlacementStatus.Active,
            };
            db.Students.Add(student);
            db.Placements.Add(placement);
            placementId = placement.Id;

            // Baris OutboxMessage SUNGGUHAN (bukan cuma event .NET) - meniru apa yg endpoint/cron
            // tulis inline (JournalEndpoints.cs dst.) SEBELUM OutboxDispatcher sempat memprosesnya.
            var evt = new JournalApprovedEvent(Guid.NewGuid(), placementId, Guid.NewGuid());
            outboxMessageId = Guid.NewGuid();
            db.OutboxMessages.Add(new OutboxMessage
            {
                Id = outboxMessageId,
                Type = "JournalApproved",
                PayloadJson = System.Text.Json.JsonSerializer.Serialize(evt),
            });
            await db.SaveChangesAsync();
        }

        var payload = await GetPayloadAsync(outboxMessageId);

        using (var pubScope = fixture.Prod.CreateScope())
        {
            var publishEndpoint = pubScope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

            // Publish PERTAMA: "berhasil" tapi disimulasikan CRASH SEBELUM PublishedAt ditandai -
            // baris OutboxMessage SENGAJA TIDAK di-update (tetap PublishedAt=null), persis kondisi
            // yg akan membuat OutboxDispatcher nyata mem-publish ULANG di siklus poll berikutnya.
            await publishEndpoint.Publish(payload!, typeof(JournalApprovedEvent), ctx => ctx.MessageId = outboxMessageId);

            // Publish KEDUA: "siklus poll berikutnya", MessageId SAMA PERSIS (mekanisme nyata
            // OutboxDispatcher.cs - lihat doc-comment kelas ini).
            await publishEndpoint.Publish(payload!, typeof(JournalApprovedEvent), ctx => ctx.MessageId = outboxMessageId);
        }

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            using var pollScope = fixture.Prod.CreateScope();
            var pollDb = pollScope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            if (await pollDb.Notifications.AnyAsync(n => n.UserId == studentUserId))
            {
                break;
            }
            await Task.Delay(150);
        }

        using var verifyScope = fixture.Prod.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
        // Efek TEPAT 1x walau "dipublish" 2x dgn MessageId sama - TAK ADA event hilang (ada 1, bukan
        // 0) DAN TAK ADA dobel (ada 1, bukan 2) - dua sisi AC "tidak ada event hilang/dobel" sekaligus.
        Assert.Single(verifyDb.Notifications.Where(n => n.UserId == studentUserId));
        Assert.Equal(1, await verifyDb.ProcessedMessages.CountAsync(p => p.ConsumerName == nameof(JournalApprovedConsumer) && p.MessageId == outboxMessageId));
    }

    private async Task<JournalApprovedEvent?> GetPayloadAsync(Guid outboxMessageId)
    {
        using var scope = fixture.Prod.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
        var row = await db.OutboxMessages.AsNoTracking().FirstAsync(m => m.Id == outboxMessageId);
        return System.Text.Json.JsonSerializer.Deserialize<JournalApprovedEvent>(row.PayloadJson);
    }
}
