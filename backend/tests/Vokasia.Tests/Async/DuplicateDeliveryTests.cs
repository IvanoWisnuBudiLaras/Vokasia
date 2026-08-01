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
/// VOK-H4-E3 §1 DuplicateDeliveryTests — "kirim message identik 2x -> efek tepat 1x", kali ini
/// lewat RabbitMQ SUNGGUHAN (bukan in-memory transport - lihat doc-comment AsyncTestFixture utk
/// perbedaan dgn ConsumerDuplicateDeliveryTests.cs H4-E1). JournalApprovedConsumer dipilih dgn
/// alasan SAMA PERSIS dgn test H4-E1 itu (efek samping Notification tak idempotent scr alami tanpa
/// guard - lihat doc-comment kelasnya).
/// </summary>
[Collection("AsyncTests")]
public class DuplicateDeliveryTests(AsyncTestFixture fixture)
{
    private static async Task WaitUntilAsync(Func<Task<bool>> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
            {
                return;
            }
            await Task.Delay(100);
        }
        Assert.True(await condition(), "Timeout menunggu kondisi terpenuhi.");
    }

    [Fact]
    public async Task JournalApproved_PublishedTwiceSameMessageId_NotificationCreatedExactlyOnce()
    {
        if (!fixture.IsDockerAvailable) return;
        var tenantId = Guid.NewGuid();
        var studentUserId = Guid.NewGuid();
        Guid placementId;

        using (var scope = fixture.Prod.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            var student = new Student { Id = Guid.NewGuid(), TenantId = tenantId, UserId = studentUserId, FullName = "Siswa Uji Duplikat RMQ", MajorId = Guid.NewGuid(), Classroom = "XII RPL 1" };
            var placement = new Placement
            {
                Id = Guid.NewGuid(), TenantId = tenantId, StudentId = student.Id,
                CompanyId = Guid.NewGuid(), PeriodId = Guid.NewGuid(), TeacherId = Guid.NewGuid(),
                Status = PlacementStatus.Active,
            };
            db.Students.Add(student);
            db.Placements.Add(placement);
            await db.SaveChangesAsync();
            placementId = placement.Id;
        }

        var messageId = Guid.NewGuid();
        var evt = new JournalApprovedEvent(Guid.NewGuid(), placementId, Guid.NewGuid());

        using (var pubScope = fixture.Prod.CreateScope())
        {
            await pubScope.ServiceProvider.GetRequiredService<IPublishEndpoint>().Publish(evt, ctx => ctx.MessageId = messageId);
        }

        // Tunggu delivery PERTAMA benar2 selesai+commit (broker RabbitMQ sungguhan - butuh jeda
        // jaringan nyata, bukan in-memory instan).
        await WaitUntilAsync(async () =>
        {
            using var scope = fixture.Prod.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            return await db.ProcessedMessages.AnyAsync(p => p.ConsumerName == nameof(JournalApprovedConsumer) && p.MessageId == messageId);
        }, TimeSpan.FromSeconds(15));

        // Redelivery SEKUENSIAL (message fisik SAMA, MessageId sama) - persis skenario "broker
        // mengirim ulang setelah timeout ack" yang jadi dasar desain IdempotencyGuard.
        using (var pubScope2 = fixture.Prod.CreateScope())
        {
            await pubScope2.ServiceProvider.GetRequiredService<IPublishEndpoint>().Publish(evt, ctx => ctx.MessageId = messageId);
        }
        await Task.Delay(2000); // beri waktu broker RabbitMQ sungguhan mengirim+consumer no-op memprosesnya.

        using var verifyScope = fixture.Prod.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
        Assert.Single(verifyDb.Notifications.Where(n => n.UserId == studentUserId));
        Assert.Equal(1, await verifyDb.ProcessedMessages.CountAsync(p => p.ConsumerName == nameof(JournalApprovedConsumer) && p.MessageId == messageId));
    }
}
