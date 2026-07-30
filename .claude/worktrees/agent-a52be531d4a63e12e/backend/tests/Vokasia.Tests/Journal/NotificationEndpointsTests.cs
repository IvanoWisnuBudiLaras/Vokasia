using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Persistence;
using Vokasia.Tests.Auth;

namespace Vokasia.Tests.Journal;

/// <summary>
/// AC VOK-H4-E1 §4 — bell notifikasi in-app, lintas SEMUA peran (`.RequireAuthorization()` bare,
/// bukan policy RBAC bernama - lihat doc-comment NotificationEndpoints.cs). Fokus suite ini:
/// scoping per-caller (tak pernah bocor notifikasi user lain) + pola privasi "404 bukan 403" utk
/// MarkRead milik user lain (SENGAJA, biar tak bocorkan KEBERADAAN notifikasi user lain).
/// </summary>
public class NotificationEndpointsTests : IClassFixture<VokasiaApiFactory>
{
    private readonly VokasiaApiFactory _factory;
    public NotificationEndpointsTests(VokasiaApiFactory factory) => _factory = factory;

    private async Task<(HttpClient Client, Guid UserId)> AuthenticatedClientAsync()
    {
        var user = await AuthTestHelpers.SeedUserAsync(_factory, "notif-user", UserRole.Student, Guid.NewGuid());
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var (accessToken, _) = await AuthTestHelpers.LoginAndExchangeAsync(client, user.Email!);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return (client, user.Id);
    }

    private async Task<Notification> SeedNotificationAsync(Guid userId, bool isRead = false, string type = "JournalApproved")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
        var notif = new Notification { Id = Guid.NewGuid(), UserId = userId, Type = type, PayloadJson = "{}", IsRead = isRead };
        db.Notifications.Add(notif);
        await db.SaveChangesAsync();
        return notif;
    }

    [Fact]
    public async Task ListMyNotifications_ReturnsOnlyCallersOwnNotifications()
    {
        var (client, userId) = await AuthenticatedClientAsync();
        await SeedNotificationAsync(userId);
        await SeedNotificationAsync(userId);
        await SeedNotificationAsync(Guid.NewGuid()); // milik user LAIN - tak boleh ikut muncul.

        var resp = await client.GetAsync("/api/notifications");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, body.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, body.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task ListMyNotifications_UnreadOnlyFilter_ExcludesAlreadyRead()
    {
        var (client, userId) = await AuthenticatedClientAsync();
        await SeedNotificationAsync(userId, isRead: false);
        await SeedNotificationAsync(userId, isRead: false);
        await SeedNotificationAsync(userId, isRead: true);

        var resp = await client.GetAsync("/api/notifications?unreadOnly=true");

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, body.GetProperty("totalCount").GetInt32());
        foreach (var item in body.GetProperty("items").EnumerateArray())
        {
            Assert.False(item.GetProperty("isRead").GetBoolean());
        }
    }

    [Fact]
    public async Task MarkRead_OwnNotification_SetsIsReadTrue()
    {
        var (client, userId) = await AuthenticatedClientAsync();
        var notif = await SeedNotificationAsync(userId);

        var resp = await client.PostAsync($"/api/notifications/{notif.Id}/read", content: null);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var verify = _factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<VokasiaDbContext>();
        var updated = await db.Notifications.FirstAsync(n => n.Id == notif.Id);
        Assert.True(updated.IsRead);
    }

    [Fact]
    public async Task MarkRead_OtherUsersNotification_Returns404NotForbidden()
    {
        // AC/pola privasi (lihat doc-comment NotificationEndpoints.MarkRead): 404, BUKAN 403 - tak
        // boleh membocorkan KE PEMANGGIL bahwa notifikasi dgn id itu memang ADA (milik orang lain).
        var (client, _) = await AuthenticatedClientAsync();
        var otherUsersNotif = await SeedNotificationAsync(Guid.NewGuid());

        var resp = await client.PostAsync($"/api/notifications/{otherUsersNotif.Id}/read", content: null);

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        using var verify = _factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<VokasiaDbContext>();
        var untouched = await db.Notifications.FirstAsync(n => n.Id == otherUsersNotif.Id);
        Assert.False(untouched.IsRead); // tak ikut ter-update walau request "berhasil ditolak".
    }

    [Fact]
    public async Task MarkRead_NonExistentId_Returns404()
    {
        var (client, _) = await AuthenticatedClientAsync();

        var resp = await client.PostAsync($"/api/notifications/{Guid.NewGuid()}/read", content: null);

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task MarkAllRead_MarksOnlyCallersUnread_LeavesOtherUsersNotificationsUntouched()
    {
        var (client, userId) = await AuthenticatedClientAsync();
        await SeedNotificationAsync(userId, isRead: false);
        await SeedNotificationAsync(userId, isRead: false);
        await SeedNotificationAsync(userId, isRead: true); // sudah dibaca - tak masuk hitungan "Updated".
        var otherUserId = Guid.NewGuid();
        var othersNotif = await SeedNotificationAsync(otherUserId, isRead: false);

        var resp = await client.PostAsync("/api/notifications/read-all", content: null);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, body.GetProperty("updated").GetInt32());

        using var verify = _factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<VokasiaDbContext>();
        Assert.DoesNotContain(db.Notifications.Where(n => n.UserId == userId), n => !n.IsRead);
        var untouchedOther = await db.Notifications.FirstAsync(n => n.Id == othersNotif.Id);
        Assert.False(untouchedOther.IsRead); // punya user LAIN tak ikut ter-mark-read.
    }
}
