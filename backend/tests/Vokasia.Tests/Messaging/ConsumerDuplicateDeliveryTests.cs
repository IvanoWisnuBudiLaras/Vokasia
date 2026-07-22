using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Domain.Events;
using Vokasia.Infrastructure.Messaging;
using Vokasia.Infrastructure.Persistence;
using Vokasia.Infrastructure.TenantContext;
using Vokasia.Worker.Consumers;

namespace Vokasia.Tests.Messaging;

/// <summary>
/// AC VOK-H4-E1 §3 (idempotency): "message sama dikirim 2x -> efek 1x", dibuktikan LEWAT BUS
/// MassTransit sungguhan (in-memory test transport, <c>AddMassTransitTestHarness</c>) - bukan cuma
/// memanggil Consume() langsung - supaya wiring consumer (AddConsumer registrasi,
/// ConsumeContext.MessageId, dst.) ikut terbukti, bukan diasumsikan.
///
/// JournalApprovedConsumer dipilih sbg subjek (bukan JournalSubmittedConsumer/StreakCounterConsumer)
/// KARENA efek sampingnya (Notifier.CreateNotification -> INSERT baris Notification baru dgn Guid
/// baru tiap panggilan) TIDAK idempotent scr alami - redelivery TANPA guard akan kelihatan JELAS
/// sbg 2 baris Notification (bukan 1) di inbox siswa. Ini PENTING: percobaan awal sesi ini memakai
/// JournalSubmittedConsumer/StreakCounterConsumer (keduanya cuma nge-SET nilai kolom, mis.
/// Rag=Green atau Streak dihitung ulang dari data yg sama) - mengulang operasi itu 2x scr alami
/// PRODUKSI HASIL SAMA walau guard dimatikan (PROMPT D: dicoba matikan guard, test TETAP hijau -
/// tanda test itu tak membuktikan apa2 soal guard). CreateNotification tak punya masalah itu.
///
/// Dua consumer TERPISAH yg menyentuh baris StudentDailyStatus yg SAMA scr genuinely-concurrent
/// (JournalSubmittedConsumer+StreakCounterConsumer) TERBUKTI (investigasi sesi ini) memicu
/// keterbatasan EF Core InMemory provider soal penegakan unique index antar DbContext instance -
/// lihat StudentDailyStatusUpsertConcurrencyTests.cs (manual, thd Postgres nyata) utk topik itu;
/// TIDAK relevan di sini krn test ini pakai SATU consumer saja.
///
/// [CATATAN teknis]: harness.Consumed/Sent/Published TIDAK bisa dipakai utk memverifikasi
/// redelivery MessageId yang SAMA - dikonfirmasi lewat dokumentasi MassTransit sendiri: "For a
/// given messageId, only the first delivery... will be tracked... Subsequent redelivery of the
/// same messageId will be discarded". Maka verifikasi kedua (setelah redelivery) lewat STATE DB
/// langsung + jeda waktu tetap, bukan lewat count harness.Consumed.
/// </summary>
public class ConsumerDuplicateDeliveryTests
{
    private static async Task<(ServiceProvider Provider, ITestHarness Harness)> BuildHarnessAsync(string dbName)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<VokasiaDbContext>(opt => opt.UseInMemoryDatabase(dbName));
        // TenantId null -> query filter tenant MATI (pola sama persis dgn ListJournalsNPlusOneVerification.cs).
        services.AddScoped<ITenantContext>(_ => new AmbientTenantContext());
        services.AddScoped<IdempotencyGuard>();
        services.AddScoped<INotifier, Notifier>();

        services.AddMassTransitTestHarness(x => x.AddConsumer<JournalApprovedConsumer>());

        var provider = services.BuildServiceProvider(validateScopes: true);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        return (provider, harness);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }
            await Task.Delay(25);
        }

        Assert.True(condition(), "Timeout menunggu kondisi terpenuhi.");
    }

    [Fact]
    public async Task JournalApproved_RedeliveredAfterFirstCommit_NotificationCreatedExactlyOnce()
    {
        var (provider, harness) = await BuildHarnessAsync($"consumer-dup-{Guid.NewGuid():N}");
        try
        {
            var tenantId = Guid.NewGuid();
            var studentUserId = Guid.NewGuid();
            Guid placementId;

            using (var scope = provider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
                var student = new Student { Id = Guid.NewGuid(), TenantId = tenantId, UserId = studentUserId, FullName = "Siswa Uji Duplikat", MajorId = Guid.NewGuid(), Classroom = "XII RPL 1" };
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

            var consumerHarness = harness.GetConsumerHarness<JournalApprovedConsumer>();
            var messageId = Guid.NewGuid();
            var journalId = Guid.NewGuid();
            var evt = new JournalApprovedEvent(journalId, placementId, Guid.NewGuid());

            await harness.Bus.Publish(evt, ctx => ctx.MessageId = messageId);
            await WaitUntilAsync(
                () => consumerHarness.Consumed.Select<JournalApprovedEvent>().Count(x => x.Context.MessageId == messageId) >= 1,
                TimeSpan.FromSeconds(10));

            using (var checkScope = provider.CreateScope())
            {
                var checkDb = checkScope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
                Assert.Single(checkDb.Notifications.Where(n => n.UserId == studentUserId));
            }

            // Redelivery SEKUENSIAL: message fisik SAMA (MessageId sama), dikirim lagi SETELAH yang
            // PERTAMA benar2 selesai diproses+commit (dibuktikan di atas) - persis skenario yg
            // didokumentasikan IdempotencyGuard.cs sbg yg dijamin desain ini.
            await harness.Bus.Publish(evt, ctx => ctx.MessageId = messageId);
            await Task.Delay(1000); // in-memory transport - beri waktu redelivery benar2 "diproses" (no-op).

            using var verifyScope = provider.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<VokasiaDbContext>();

            // Properti INTI yg dibuktikan: TETAP SATU notifikasi walau message fisik yg sama
            // "dikirim" 2x - kalau guard tak bekerja, di sini akan ada 2 baris Notification.
            Assert.Single(verifyDb.Notifications.Where(n => n.UserId == studentUserId));
            Assert.Equal(1, await verifyDb.ProcessedMessages.CountAsync(p => p.ConsumerName == nameof(JournalApprovedConsumer) && p.MessageId == messageId));
        }
        finally
        {
            await harness.Stop();
            await provider.DisposeAsync();
        }
    }
}
