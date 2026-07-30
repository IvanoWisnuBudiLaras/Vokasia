using Microsoft.EntityFrameworkCore;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Infrastructure.Scheduling;

/// <summary>
/// VOK-H4-E1 — helper upsert StudentDailyStatus bersama, dipakai JournalSubmittedConsumer (set
/// Rag=Green) & StreakCounterConsumer (set Streak) - KEDUANYA bereaksi ke event JournalSubmitted
/// yang SAMA & bisa berjalan hampir bersamaan (2 consumer independen), menyentuh baris yang SAMA
/// (StudentId,PeriodId,Date - unique index di VokasiaDbContext). Tanpa helper ini, race INSERT
/// bersamaan akan membuat SALAH SATU gagal dgn DbUpdateException mentah (unique violation) tanpa
/// penanganan - ditangkap+retry-sbg-update di sini supaya kedua consumer tetap benar walau urutan
/// eksekusinya tak terjamin.
/// </summary>
public static class StudentDailyStatusUpsert
{
    public static async Task ApplyAsync(
        VokasiaDbContext db, Guid tenantId, Guid studentId, Guid periodId, DateOnly date,
        Action<StudentDailyStatus> apply, CancellationToken ct)
    {
        var existing = await db.StudentDailyStatuses
            .FirstOrDefaultAsync(x => x.StudentId == studentId && x.PeriodId == periodId && x.Date == date, ct);

        if (existing is not null)
        {
            apply(existing);
            await db.SaveChangesAsync(ct);
            return;
        }

        var created = new StudentDailyStatus
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            StudentId = studentId,
            PeriodId = periodId,
            Date = date,
        };
        apply(created);
        db.StudentDailyStatuses.Add(created);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            db.Entry(created).State = EntityState.Detached;
            var raceWinner = await db.StudentDailyStatuses
                .FirstAsync(x => x.StudentId == studentId && x.PeriodId == periodId && x.Date == date, ct);
            apply(raceWinner);
            await db.SaveChangesAsync(ct);
        }
    }
}
