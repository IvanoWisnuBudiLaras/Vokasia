using Microsoft.EntityFrameworkCore;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Persistence;
using Vokasia.Infrastructure.TenantContext;

namespace Vokasia.Tests.Journal;

/// <summary>
/// AC VOK-H3-E1 §4 DoD: "Given ListJournals 90 hari, Then 1 query utama (log EF dibuktikan, tanpa
/// N+1)". EF Core InMemory provider (dipakai <see cref="Auth.VokasiaApiFactory"/>, seluruh suite
/// lain) TIDAK menghasilkan log "Executed DbCommand" yang bermakna (tak ada SQL sungguhan) - klaim
/// "1 query" hanya bisa dibuktikan thd provider relasional NYATA (Npgsql/Postgres), persis
/// keterbatasan yang sudah didokumentasikan VokasiaApiFactory sendiri ("bukan pengganti integration
/// test Testcontainers penuh, itu wilayah H5-E3").
///
/// Test ini SENGAJA di-<c>Skip</c> secara default (bukan bagian suite CI/dotnet-test rutin, yang
/// harus tetap portable tanpa Postgres hidup) - jalankan MANUAL (hapus Skip sementara) saat
/// Postgres docker-compose sedang healthy (lihat DECISIONS.md D23 - Gate M0) utk verifikasi ulang
/// kalau proyeksi ListJournals (JournalEndpoints.cs) berubah. Dijalankan SEKALI scr manual saat
/// H3-E1 ditulis, thd Postgres nyata (localhost:5432, connection string fallback yang sama dgn
/// DependencyInjection.cs) - hasil: TEPAT 1 "Executed DbCommand" per pemanggilan proyeksi (subquery
/// Photos/CompetencyIds diterjemahkan Npgsql provider jadi bagian query utama, bukan round-trip
/// terpisah) - didokumentasikan di DECISIONS.md, bukan diklaim tanpa bukti.
/// </summary>
public class ListJournalsNPlusOneVerification
{
    [Fact(Skip = "Manual-only: butuh Postgres docker-compose hidup di localhost:5432, lihat doc-comment kelas ini. Diverifikasi PASS scr manual 2026-07-21 (DECISIONS.md D24) - 1 Executed DbCommand persis, thd Postgres nyata via Gate M0 stack.")]
    public async Task ListJournalsProjection_Against90DaysOfData_ExecutesExactlyOneQuery()
    {
        var commandCount = 0;
        var options = new DbContextOptionsBuilder<VokasiaDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=vokasia;Username=vokasia;Password=vokasia_dev")
            .LogTo(line =>
            {
                if (line.Contains("Executed DbCommand"))
                {
                    Interlocked.Increment(ref commandCount);
                }
            })
            .Options;

        await using var db = new VokasiaDbContext(options, new AmbientTenantContext()); // TenantId null -> filter tenant mati, lihat catatan JournalCronJobs.

        var tenantId = Guid.NewGuid();
        var placement = new Placement { Id = Guid.NewGuid(), TenantId = tenantId, StudentId = Guid.NewGuid(), CompanyId = Guid.NewGuid(), PeriodId = Guid.NewGuid(), TeacherId = Guid.NewGuid(), Status = PlacementStatus.Active };
        db.Placements.Add(placement);

        var entries = new List<JournalEntry>();
        for (var i = 0; i < 90; i++)
        {
            var slot = new JournalSlot { Id = Guid.NewGuid(), TenantId = tenantId, PlacementId = placement.Id, Date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-i), Status = JournalSlotStatus.Filled };
            var entry = new JournalEntry { Id = Guid.NewGuid(), TenantId = tenantId, SlotId = slot.Id, PlacementId = placement.Id, Text = $"Jurnal hari ke-{i}", Status = JournalEntryStatus.Approved, SubmittedAt = DateTimeOffset.UtcNow.AddDays(-i) };
            db.JournalSlots.Add(slot);
            db.JournalEntries.Add(entry);
            entries.Add(entry);
            db.JournalPhotos.Add(new JournalPhoto { Id = Guid.NewGuid(), TenantId = tenantId, JournalEntryId = entry.Id, ObjectKey = $"tenant/{tenantId}/journal/foto-{i}.jpg", Status = PhotoStatus.Processed });
        }
        await db.SaveChangesAsync();

        commandCount = 0; // reset - hanya hitung query dari proyeksi ListJournals, bukan seeding di atas.

        var placementIds = new List<Guid> { placement.Id };
        var items = await db.JournalEntries.AsNoTracking()
            .Where(e => placementIds.Contains(e.PlacementId))
            .OrderByDescending(e => e.SubmittedAt)
            .Take(20)
            .Select(e => new
            {
                e.Id,
                e.Text,
                Photos = db.JournalPhotos.Where(p => p.JournalEntryId == e.Id).Select(p => p.ObjectKey).ToList(),
                CompetencyIds = db.JournalCompetencies.Where(jc => jc.JournalEntryId == e.Id).Select(jc => jc.CompetencyId).ToList(),
            })
            .ToListAsync();

        Assert.Equal(20, items.Count);
        Assert.Equal(1, commandCount);
    }
}
