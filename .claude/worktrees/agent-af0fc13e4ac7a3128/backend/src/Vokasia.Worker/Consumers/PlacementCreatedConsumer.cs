using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vokasia.Domain.Common;
using Vokasia.Domain.Events;
using Vokasia.Infrastructure.Messaging;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Worker.Consumers;

/// <summary>
/// VOK-H4-E1 §2 — welcome pack notifikasi siswa+guru saat placement baru dibuat. TeacherId TIDAK
/// ada di payload (lihat OutboxEventContracts.cs) - di-lookup di sini via query Placement by Id.
/// Siswa TANPA akun login (Student.UserId null - lazim: siswa diimpor CSV dulu, akun dibuat
/// belakangan, lihat StudentEntities.cs) dilewati SENYAP utk bagian siswa saja - guru (TeacherId =
/// AppUser.Id LANGSUNG, guru SELALU py akun sejak dibuat CreateDemoUserAsync/InviteUser) tetap
/// dinotifikasi.
/// </summary>
public class PlacementCreatedConsumer(VokasiaDbContext db, IdempotencyGuard guard, INotifier notifier, ILogger<PlacementCreatedConsumer> logger)
    : IConsumer<PlacementCreatedEvent>
{
    public const string Name = nameof(PlacementCreatedConsumer);

    public async Task Consume(ConsumeContext<PlacementCreatedEvent> context)
    {
        var ct = context.CancellationToken;
        var messageId = context.MessageId ?? Guid.Empty;

        if (!await guard.EnsureNotProcessedAsync(Name, messageId, ct))
        {
            logger.LogInformation("{Consumer}: pesan {MessageId} sudah diproses sebelumnya, dilewati.", Name, messageId);
            return;
        }

        var msg = context.Message;

        var placement = await db.Placements.AsNoTracking().FirstOrDefaultAsync(p => p.Id == msg.PlacementId, ct);
        if (placement is null)
        {
            logger.LogWarning("{Consumer}: Placement {PlacementId} tak ditemukan - welcome pack dilewati.", Name, msg.PlacementId);
            await db.SaveChangesAsync(ct);
            return;
        }

        var studentUserId = await db.Students.AsNoTracking().Where(s => s.Id == msg.StudentId).Select(s => s.UserId).FirstOrDefaultAsync(ct);
        if (studentUserId.HasValue)
        {
            notifier.CreateNotification(studentUserId.Value, NotificationType.PlacementWelcome, new { PlacementId = msg.PlacementId, msg.CompanyId });
        }

        // Guru = AppUser langsung (Placement.TeacherId == AppUser.Id, lihat doc-comment PlacementEntities.cs) - selalu punya akun.
        notifier.CreateNotification(placement.TeacherId, NotificationType.PlacementWelcome, new { PlacementId = msg.PlacementId, msg.StudentId, msg.CompanyId });

        await db.SaveChangesAsync(ct);
        logger.LogInformation("{Consumer}: welcome pack placement {PlacementId} terkirim (siswa+guru).", Name, msg.PlacementId);
    }
}
