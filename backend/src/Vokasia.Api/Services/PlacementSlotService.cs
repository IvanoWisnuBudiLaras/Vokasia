using Microsoft.EntityFrameworkCore;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Api.Services;

public static class PlacementSlotService
{
    public static async Task EnsureSlotsForPlacementAsync(VokasiaDbContext db, Placement placement, CancellationToken ct = default)
    {
        var period = await db.Periods.AsNoTracking().FirstOrDefaultAsync(p => p.Id == placement.PeriodId, ct);
        if (period is null) return;

        var existingDates = await db.JournalSlots.AsNoTracking()
            .Where(s => s.PlacementId == placement.Id)
            .Select(s => s.Date)
            .ToHashSetAsync(ct);

        var holidays = await db.Holidays.AsNoTracking()
            .Where(h => h.PeriodId == placement.PeriodId)
            .Select(h => h.Date)
            .ToHashSetAsync(ct);

        var toCreate = new List<JournalSlot>();
        var curr = period.StartDate;
        while (curr <= period.EndDate)
        {
            if (curr.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday && !holidays.Contains(curr))
            {
                if (!existingDates.Contains(curr))
                {
                    toCreate.Add(new JournalSlot
                    {
                        Id = Guid.NewGuid(),
                        TenantId = placement.TenantId,
                        PlacementId = placement.Id,
                        Date = curr,
                        Status = JournalSlotStatus.Empty
                    });
                }
            }
            curr = curr.AddDays(1);
        }

        if (toCreate.Count > 0)
        {
            db.JournalSlots.AddRange(toCreate);
            await db.SaveChangesAsync(ct);
        }
    }
}
