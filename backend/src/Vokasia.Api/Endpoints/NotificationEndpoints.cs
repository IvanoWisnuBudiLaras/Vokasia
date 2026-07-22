using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vokasia.Domain.Common;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Api.Endpoints;

/// <summary>
/// VOK-H4-E1 §4 — bell notifikasi FE, dipakai LINTAS SEMUA peran (siswa/mentor/guru/admin semua
/// bisa terima notifikasi - lihat NotificationType.cs & consumer H4-E1). TIDAK ada policy RBAC
/// bernama yang cocok "siapa saja asalkan login" (7 policy di RbacPolicies.cs semua scoped
/// role/tenant tertentu, TenantMember eksplisit MENGECUALIKAN Mentor/SuperAdmin) - pakai
/// `.RequireAuthorization()` BARE (default: hanya butuh IsAuthenticated), pola yang SUDAH dipakai
/// endpoint mentor JournalEndpoints (GetPendingApprovals dst.) dgn alasan sama: scoping sungguhan
/// terjadi DI DALAM handler (filter WHERE UserId==caller), bukan lewat nama policy.
/// </summary>
public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/notifications").WithTags("Notifications");

        group.MapGet("/", ListMyNotifications).RequireAuthorization();
        group.MapPost("/{id:guid}/read", MarkRead).RequireAuthorization();
        group.MapPost("/read-all", MarkAllRead).RequireAuthorization();

        return app;
    }

    private static Guid? CallerUserId(ITenantContext tenant) => tenant.UserId;

    private static async Task<IResult> ListMyNotifications(
        VokasiaDbContext db, ITenantContext tenant, CancellationToken ct,
        [FromQuery] bool unreadOnly = false, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var userId = CallerUserId(tenant);
        if (userId is null)
        {
            return Results.Forbid();
        }

        var query = db.Notifications.AsNoTracking().Where(n => n.UserId == userId.Value);
        if (unreadOnly)
        {
            query = query.Where(n => !n.IsRead);
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(n => new NotificationDto(n.Id, n.Type, n.PayloadJson, n.IsRead, n.CreatedAt))
            .ToListAsync(ct);

        return Results.Ok(new Paged<NotificationDto>(items, page, pageSize, total));
    }

    private static async Task<IResult> MarkRead(Guid id, VokasiaDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        var userId = CallerUserId(tenant);
        if (userId is null)
        {
            return Results.Forbid();
        }

        var notif = await db.Notifications.FirstOrDefaultAsync(n => n.Id == id, ct);
        if (notif is null || notif.UserId != userId.Value)
        {
            // Bukan milik caller - "tidak ditemukan" (BUKAN 403) supaya tak bocorkan keberadaan
            // notifikasi user lain (sama pola privasi dgn Placement lintas tenant di JournalEndpoints).
            return Results.NotFound();
        }

        notif.IsRead = true;
        await db.SaveChangesAsync(ct);
        return Results.Ok(new NotificationDto(notif.Id, notif.Type, notif.PayloadJson, notif.IsRead, notif.CreatedAt));
    }

    private static async Task<IResult> MarkAllRead(VokasiaDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        var userId = CallerUserId(tenant);
        if (userId is null)
        {
            return Results.Forbid();
        }

        var unread = await db.Notifications.Where(n => n.UserId == userId.Value && !n.IsRead).ToListAsync(ct);
        foreach (var n in unread)
        {
            n.IsRead = true;
        }
        await db.SaveChangesAsync(ct);

        return Results.Ok(new { Updated = unread.Count });
    }
}
