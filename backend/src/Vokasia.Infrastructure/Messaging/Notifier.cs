using System.Text.Json;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Infrastructure.Messaging;

/// <summary>
/// VOK-H4-E1 §4 — CreateNotification(userId, type, payload): SATU PINTU notif in-app (ticket
/// sendiri). Refactor 2 titik yang SEBELUMNYA insert db.Notifications.Add(...) langsung
/// (JournalEndpoints.AddTeacherComment, JournalCronJobs.RemindEmptyJournals) supaya konsisten -
/// lihat commit terkait.
///
/// TIDAK memanggil SaveChangesAsync sendiri (sama filosofi IdempotencyGuard) - caller (endpoint
/// atau consumer) menggabungkan baris Notification ini dgn efek sampingnya sendiri dalam SATU
/// SaveChanges, satu transaksi.
/// </summary>
public interface INotifier
{
    void CreateNotification(Guid userId, NotificationType type, object payload);
}

public class Notifier(VokasiaDbContext db) : INotifier
{
    public void CreateNotification(Guid userId, NotificationType type, object payload)
    {
        db.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = type.ToString(),
            PayloadJson = JsonSerializer.Serialize(payload),
        });
    }
}
