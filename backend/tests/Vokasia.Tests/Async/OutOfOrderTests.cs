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
/// VOK-H4-E3 §1 OutOfOrderTests — "JournalApproved tiba SEBELUM JournalSubmitted terproyeksi ->
/// consumer tidak crash; status akhir konsisten". JournalApprovedConsumer & JournalSubmittedConsumer
/// dikonfirmasi (baca kode, lihat AsyncTestFixture) SAMA SEKALI TIDAK saling bergantung pada efek
/// samping satu sama lain - keduanya look-up Placement/Student/JournalSlot LANGSUNG dari ground-truth
/// DB (bukan dari proyeksi consumer lain) - jadi "keluar urutan" di sini murni membuktikan TIDAK ADA
/// crash/exception/state tak konsisten, BUKAN membuktikan ada logika "tunggu dulu" tersembunyi
/// (memang tak ada, by design - dicatat eksplisit, bukan diasumsikan berhasil krn kebetulan).
/// </summary>
[Collection("AsyncTests")]
public class OutOfOrderTests(AsyncTestFixture fixture)
{
    private static async Task WaitUntilAsync(Func<Task<bool>> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (await condition()) return;
            await Task.Delay(100);
        }
        Assert.True(await condition(), "Timeout menunggu kondisi terpenuhi.");
    }

    [Fact]
    public async Task JournalApprovedBeforeJournalSubmitted_BothConsumersSucceed_FinalStateConsistent()
    {
        if (!fixture.IsDockerAvailable) return;
        var tenantId = Guid.NewGuid();
        var studentUserId = Guid.NewGuid();
        var periodId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        Guid placementId;
        Guid slotId;
        var slotDate = DateOnly.FromDateTime(DateTime.UtcNow);

        using (var scope = fixture.Prod.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            var student = new Student { Id = studentId, TenantId = tenantId, UserId = studentUserId, FullName = "Siswa Uji Out-of-Order", MajorId = Guid.NewGuid(), Classroom = "XII RPL 2" };
            var placement = new Placement
            {
                Id = Guid.NewGuid(), TenantId = tenantId, StudentId = studentId,
                CompanyId = Guid.NewGuid(), PeriodId = periodId, TeacherId = Guid.NewGuid(),
                Status = PlacementStatus.Active,
            };
            var slot = new JournalSlot { Id = Guid.NewGuid(), TenantId = tenantId, PlacementId = placement.Id, Date = slotDate, Status = JournalSlotStatus.Filled };
            db.Students.Add(student);
            db.Placements.Add(placement);
            db.JournalSlots.Add(slot);
            await db.SaveChangesAsync();
            placementId = placement.Id;
            slotId = slot.Id;
        }

        var journalId = Guid.NewGuid();

        // URUTAN SENGAJA DIBALIK: Approved dipublish DULUAN, Submitted BELAKANGAN.
        using (var pubScope = fixture.Prod.CreateScope())
        {
            var publishEndpoint = pubScope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
            await publishEndpoint.Publish(new JournalApprovedEvent(journalId, placementId, slotId), ctx => ctx.MessageId = Guid.NewGuid());
            await publishEndpoint.Publish(new JournalSubmittedEvent(journalId, slotId, placementId, false), ctx => ctx.MessageId = Guid.NewGuid());
        }

        await WaitUntilAsync(async () =>
        {
            using var scope = fixture.Prod.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            var hasNotif = await db.Notifications.AnyAsync(n => n.UserId == studentUserId);
            var hasStatus = await db.StudentDailyStatuses.AnyAsync(s => s.StudentId == studentId && s.PeriodId == periodId && s.Date == slotDate && s.Rag == RagStatus.Green);
            return hasNotif && hasStatus;
        }, TimeSpan.FromSeconds(15));

        // Tak ada exception yang lolos sampai sini (WaitUntilAsync sendiri akan gagal duluan kalau
        // consumer crash tak pernah menghasilkan efek) - keduanya SUKSES walau urutan dibalik.
        using var verifyScope = fixture.Prod.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
        Assert.Single(verifyDb.Notifications.Where(n => n.UserId == studentUserId));
        var finalStatus = await verifyDb.StudentDailyStatuses.SingleAsync(s => s.StudentId == studentId && s.PeriodId == periodId && s.Date == slotDate);
        Assert.Equal(RagStatus.Green, finalStatus.Rag);
    }
}
