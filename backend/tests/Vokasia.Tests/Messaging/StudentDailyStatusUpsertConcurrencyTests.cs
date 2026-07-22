using Microsoft.EntityFrameworkCore;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Persistence;
using Vokasia.Infrastructure.Scheduling;
using Vokasia.Infrastructure.TenantContext;

namespace Vokasia.Tests.Messaging;

/// <summary>
/// AC VOK-H4-E1 §2 (StudentDailyStatusUpsert doc-comment): JournalSubmittedConsumer +
/// StreakCounterConsumer bereaksi ke event YANG SAMA, MassTransit bisa mendispatch keduanya
/// hampir bersamaan (2 DbContext/scope TERPISAH), menyentuh baris StudentDailyStatus yang SAMA
/// (unique index StudentId+PeriodId+Date) - helper ini SEHARUSNYA membuat keduanya konvergen ke
/// SATU baris (catch DbUpdateException -> detach -> refetch pemenang race -> apply -> save).
///
/// [TEMUAN PENTING, dicatat bukan diam-diam dihindari]: skenario ini TIDAK BISA diuji lewat EF
/// Core InMemory provider (dipakai suite lain, portable tanpa Postgres hidup) - dibuktikan lewat
/// investigasi sesi ini: menjalankan konkurensi yang SAMA (2 VokasiaDbContext terpisah, Task.WhenAll)
/// thd InMemory provider menghasilkan 2 BARIS DUPLIKAT (bukan konvergen ke 1) - InMemory provider
/// TIDAK menegakkan unique index scr andal antar SaveChanges dari context instance berbeda yang
/// genuinely concurrent (Microsoft sendiri mendokumentasikan InMemory provider tak dirancang utk
/// menguji perilaku concurrency sungguhan). Test yang SAMA PERSIS, dijalankan thd Postgres NYATA
/// (localhost:5432, docker-compose) - 30/30 iterasi konvergen benar ke 1 baris. Kesimpulan: kode
/// produksi (StudentDailyStatusUpsert) BENAR, keterbatasan ada di test double InMemory - pola yang
/// SAMA persis dgn ListJournalsNPlusOneVerification.cs (beberapa hal hanya bisa dibuktikan thd
/// provider relasional nyata). Test ini SENGAJA Skip default (portabilitas suite CI), jalankan
/// MANUAL saat docker-compose Postgres healthy.
/// </summary>
public class StudentDailyStatusUpsertConcurrencyTests
{
    [Fact(Skip = "Manual-only: butuh Postgres docker-compose hidup di localhost:5432 (lihat doc-comment kelas ini). Diverifikasi PASS scr manual 2026-07-22 - 30/30 iterasi konvergen ke 1 baris thd Postgres nyata.")]
    public async Task ConcurrentApplyAsync_TwoSeparateDbContexts_SameKey_ConvergesToOneRow()
    {
        var options = new DbContextOptionsBuilder<VokasiaDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=vokasia;Username=vokasia;Password=vokasia_dev")
            .Options;

        var failures = new List<string>();
        const int iterations = 30;
        for (var i = 0; i < iterations; i++)
        {
            var tenantId = Guid.NewGuid();
            var studentId = Guid.NewGuid();
            var periodId = Guid.NewGuid();
            var date = new DateOnly(2026, 7, 20);

            await using (var db1 = new VokasiaDbContext(options, new AmbientTenantContext()))
            await using (var db2 = new VokasiaDbContext(options, new AmbientTenantContext()))
            {
                // Mimik persis JournalSubmittedConsumer (set Rag) + StreakCounterConsumer (set
                // Streak) yang bereaksi ke event SAMA, benar2 concurrent (Task.WhenAll, bukan
                // sekadar berurutan cepat) lewat 2 DbContext/koneksi TERPISAH.
                await Task.WhenAll(
                    StudentDailyStatusUpsert.ApplyAsync(db1, tenantId, studentId, periodId, date, s => s.Rag = RagStatus.Green, default),
                    StudentDailyStatusUpsert.ApplyAsync(db2, tenantId, studentId, periodId, date, s => s.Streak = 1, default));
            }

            await using var verify = new VokasiaDbContext(options, new AmbientTenantContext());
            var rows = await verify.StudentDailyStatuses
                .Where(s => s.StudentId == studentId && s.PeriodId == periodId && s.Date == date)
                .ToListAsync();
            if (rows.Count != 1)
            {
                failures.Add($"iter {i}: rows.Count={rows.Count}: " + string.Join(" | ", rows.Select(r => $"Id={r.Id} Rag={r.Rag} Streak={r.Streak}")));
            }
            else
            {
                // Baris tunggal itu HARUS membawa KEDUA mutasi (Rag DAN Streak) - bukan cuma salah satu
                // menang lantas yang lain hilang.
                if (rows[0].Rag != RagStatus.Green || rows[0].Streak != 1)
                {
                    failures.Add($"iter {i}: baris konvergen tapi mutasi tak lengkap: Rag={rows[0].Rag} Streak={rows[0].Streak}");
                }
            }

            verify.StudentDailyStatuses.RemoveRange(rows);
            await verify.SaveChangesAsync();
        }

        Assert.True(failures.Count == 0, $"{failures.Count}/{iterations} iterasi gagal konvergen ke 1 baris lengkap:\n" + string.Join("\n", failures));
    }
}
