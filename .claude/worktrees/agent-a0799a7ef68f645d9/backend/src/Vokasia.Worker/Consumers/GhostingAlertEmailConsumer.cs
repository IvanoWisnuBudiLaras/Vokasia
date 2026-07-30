using System.Security.Cryptography;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vokasia.Domain.Common;
using Vokasia.Domain.Events;
using Vokasia.Infrastructure.Email;
using Vokasia.Infrastructure.Messaging;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Worker.Consumers;

/// <summary>
/// VOK-H4-E3 §2 "wire ke consumer H4-E1: ghosting → kirim email nyata" — consumer BARU utk
/// <see cref="GhostingAlertEmailRequestedEvent"/> (Type CLR + registrasi TypeRegistry JUGA baru di
/// ticket ini - lihat doc-comment event itu sendiri utk gap yg ditutup).
///
/// Penerima = SAMA PERSIS spt notifikasi in-app yg sudah dibuat JournalCronJobs.FlagGhostingStudents
/// SENDIRI (guru pembimbing + SEMUA TenantAdmin tenant itu) - payload event hanya bawa PlacementId
/// (thin pointer), consumer join Placement->TenantId/TeacherId/CompanyId lalu query admin serupa.
///
/// [PENTING utk idempotency]: SATU pesan ini bisa berarti BANYAK email (1 guru + N admin) - kalau
/// SEMUA pakai IdempotencyKey = context.MessageId mentah yg SAMA, penerima ke-2/ke-3 akan SALAH
/// terdeteksi "sudah terkirim" (krn SentEmail utk kunci itu sudah ada dari penerima ke-1) walau
/// belum pernah dikirim ke MEREKA. Kunci per-penerima diturunkan DETERMINISTIK dari
/// (MessageId, UserId) via MD5 (bukan utk keamanan, murni derivasi Guid stabil 16-byte) - re-delivery
/// pesan yg SAMA menghasilkan kunci yg SAMA per penerima, penerima BERBEDA dpt kunci BERBEDA.
/// </summary>
public class GhostingAlertEmailConsumer(VokasiaDbContext db, IdempotencyGuard guard, IEmailSender emailSender, ILogger<GhostingAlertEmailConsumer> logger)
    : IConsumer<GhostingAlertEmailRequestedEvent>
{
    public const string Name = nameof(GhostingAlertEmailConsumer);

    public async Task Consume(ConsumeContext<GhostingAlertEmailRequestedEvent> context)
    {
        var ct = context.CancellationToken;
        var messageId = context.MessageId ?? Guid.Empty;

        if (!await guard.EnsureNotProcessedAsync(Name, messageId, ct))
        {
            logger.LogInformation("{Consumer}: pesan {MessageId} sudah diproses sebelumnya, dilewati.", Name, messageId);
            return;
        }

        var msg = context.Message;

        var placement = await db.Placements.AsNoTracking()
            .Where(p => p.Id == msg.PlacementId)
            .Select(p => new { p.TenantId, p.TeacherId, p.CompanyId })
            .FirstOrDefaultAsync(ct);

        if (placement is null)
        {
            logger.LogWarning("{Consumer}: Placement {PlacementId} tak ditemukan - alert dilewati.", Name, msg.PlacementId);
            await db.SaveChangesAsync(ct);
            return;
        }

        var companyName = await db.Companies.AsNoTracking().Where(c => c.Id == placement.CompanyId).Select(c => c.Name).FirstOrDefaultAsync(ct) ?? "-";

        var recipientIds = new List<Guid> { placement.TeacherId };
        var adminIds = await db.Users.AsNoTracking()
            .Where(u => u.TenantId == placement.TenantId && u.Role == UserRole.TenantAdmin)
            .Select(u => u.Id)
            .ToListAsync(ct);
        recipientIds.AddRange(adminIds);

        var recipients = await db.Users.AsNoTracking()
            .Where(u => recipientIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Email })
            .ToListAsync(ct);

        const string dashboardUrl = "https://vokasia.local/app"; // [ASSUMPTION] belum ada base URL publik terkonfigurasi - lihat DECISIONS.md.
        var (subject, html, text) = EmailTemplateRenderer.GhostingAlert(msg.StudentName, companyName, msg.Days, dashboardUrl);

        var sentCount = 0;
        foreach (var recipient in recipients)
        {
            if (string.IsNullOrWhiteSpace(recipient.Email))
            {
                continue;
            }

            var perRecipientKey = DerivePerRecipientKey(messageId, recipient.Id);
            var sent = await emailSender.SendAsync(new EmailMessage(recipient.Email, "GhostingAlert", subject, html, text, perRecipientKey), ct);
            if (sent)
            {
                sentCount++;
            }
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("{Consumer}: ghosting alert placement {PlacementId} -> {Sent}/{Total} email terkirim.", Name, msg.PlacementId, sentCount, recipients.Count);
    }

    /// <summary>Turunan Guid deterministik (MessageId,UserId) -> 16 byte MD5. Lihat doc-comment kelas.</summary>
    private static Guid DerivePerRecipientKey(Guid messageId, Guid userId)
    {
        Span<byte> combined = stackalloc byte[32];
        messageId.TryWriteBytes(combined[..16]);
        userId.TryWriteBytes(combined[16..]);
        Span<byte> hash = stackalloc byte[16];
        MD5.HashData(combined, hash);
        return new Guid(hash);
    }
}
