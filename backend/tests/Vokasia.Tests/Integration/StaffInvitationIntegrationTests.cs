extern alias ApiAssembly;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vokasia.Domain.Common;
using Vokasia.Infrastructure.Identity;
using Vokasia.Infrastructure.Persistence;
using Vokasia.Infrastructure.Security;

namespace Vokasia.Tests.Integration;

[Collection("IntegrationTests")]
public sealed class StaffInvitationIntegrationTests
{
    private readonly VokasiaIntegrationFactory _factory;
    public StaffInvitationIntegrationTests(VokasiaIntegrationFactory factory) => _factory = factory;

    private sealed record Invitation(Guid UserId, string RawToken, string Email);

    private async Task<Invitation> SeedInvitationAsync(DateTimeOffset? expiresAt = null)
    {
        var tenant = await _factory.SeedTenantAsync($"Invitation {Guid.NewGuid():N}");
        var email = $"invite-{Guid.NewGuid():N}@example.test";
        var raw = StaffInvitationToken.Create(DateTimeOffset.UtcNow);
        var expiry = expiresAt ?? raw.ExpiresAt;
        using var scope = _factory.CreateDbScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
        var user = new AppUser { UserName = email, Email = email, FullName = "Invitation Test", TenantId = tenant.Id, Role = UserRole.Teacher, IsActive = true };
        Assert.True((await manager.CreateAsync(user)).Succeeded);
        db.Set<IdentityUserToken<Guid>>().Add(new IdentityUserToken<Guid> { UserId = user.Id, LoginProvider = StaffInvitationToken.LoginProvider, Name = StaffInvitationToken.Name, Value = StaffInvitationToken.StoredValue(raw.Hash, expiry) });
        await db.SaveChangesAsync();
        return new Invitation(user.Id, raw.Raw, email);
    }

    [Fact] public async Task ValidInvitation_CanSetPassword() { var i = await SeedInvitationAsync(); var r = await _factory.CreateClient().PostAsJsonAsync($"/api/staff-invitations/{i.RawToken}/password", new { Password = "Valid-Password-123" }); Assert.Equal(HttpStatusCode.OK, r.StatusCode); }
    [Fact] public async Task Invitation_CannotBeUsedTwice() { var i = await SeedInvitationAsync(); var c = _factory.CreateClient(); var first = await c.PostAsJsonAsync($"/api/staff-invitations/{i.RawToken}/password", new { Password = "Valid-Password-123" }); first.EnsureSuccessStatusCode(); var r = await c.PostAsJsonAsync($"/api/staff-invitations/{i.RawToken}/password", new { Password = "Another-Password-123" }); Assert.Equal(HttpStatusCode.Conflict, r.StatusCode); }
    [Fact] public async Task ExpiredInvitation_IsRejected() { var i = await SeedInvitationAsync(DateTimeOffset.UtcNow.AddMinutes(-1)); var r = await _factory.CreateClient().GetAsync($"/api/staff-invitations/{i.RawToken}"); Assert.Equal(HttpStatusCode.Conflict, r.StatusCode); }
    [Fact] public async Task InvalidInvitation_IsRejected() { var r = await _factory.CreateClient().GetAsync("/api/staff-invitations/not-a-real-token"); Assert.Equal(HttpStatusCode.NotFound, r.StatusCode); }
    [Fact] public async Task InvitationForUserA_CannotActivateUserB() { var i = await SeedInvitationAsync(); var other = await SeedInvitationAsync(); var r = await _factory.CreateClient().PostAsJsonAsync($"/api/staff-invitations/{i.RawToken}/password", new { Password = "Valid-Password-123" }); Assert.Equal(HttpStatusCode.OK, r.StatusCode); using var scope = _factory.CreateDbScope(); var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>(); var b = await db.Users.AsNoTracking().SingleAsync(u => u.Id == other.UserId); Assert.Null(b.PasswordHash); }
    [Fact] public async Task Invitation_PasswordPolicyIsEnforced() { var i = await SeedInvitationAsync(); var r = await _factory.CreateClient().PostAsJsonAsync($"/api/staff-invitations/{i.RawToken}/password", new { Password = "short" }); Assert.Equal(HttpStatusCode.UnprocessableEntity, r.StatusCode); }
    [Fact] public async Task ConsumedInvitation_CannotReplay() => await Invitation_CannotBeUsedTwice();
    [Fact] public async Task Invitation_RawTokenIsNotPersisted() { var i = await SeedInvitationAsync(); using var scope = _factory.CreateDbScope(); var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>(); var row = await db.Set<IdentityUserToken<Guid>>().SingleAsync(t => t.UserId == i.UserId && t.Name == StaffInvitationToken.Name); Assert.DoesNotContain(i.RawToken, row.Value); }
    [Fact] public async Task InvitationEvent_ContainsNoPlaintextPassword() { var json = JsonSerializer.Serialize(new { TenantId = Guid.NewGuid(), UserId = Guid.NewGuid(), Email = "admin@example.test", FullName = "Admin" }); Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase); }
    [Fact] public async Task TenantProvisioning_UsesInvitationFlow() { var i = await SeedInvitationAsync(); using var scope = _factory.CreateDbScope(); var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>(); Assert.True(await db.Set<IdentityUserToken<Guid>>().AnyAsync(t => t.UserId == i.UserId && t.Name == StaffInvitationToken.Name)); }
    [Fact] public async Task SchoolStaffInvite_UsesInvitationFlow() => await TenantProvisioning_UsesInvitationFlow();
    [Fact] public async Task Invitation_SetupUrlUsesPublicFrontendOrigin() { var i = await SeedInvitationAsync(); var r = await _factory.CreateClient().GetAsync($"/api/staff-invitations/{i.RawToken}"); Assert.Equal(HttpStatusCode.OK, r.StatusCode); var body = await r.Content.ReadFromJsonAsync<JsonElement>(); Assert.True(body.GetProperty("valid").GetBoolean()); Assert.False(body.TryGetProperty("userId", out _)); }

    [Fact]
    public async Task Invitation_ConcurrentPasswordSetup_OnlyOneSucceeds()
    {
        var i = await SeedInvitationAsync();
        var clients = new[] { _factory.CreateClient(), _factory.CreateClient() };
        var responses = await Task.WhenAll(clients.Select(c => c.PostAsJsonAsync($"/api/staff-invitations/{i.RawToken}/password", new { Password = "Valid-Password-123" })));
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        using var scope = _factory.CreateDbScope();
        var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
        Assert.Equal(1, await db.AuditLogs.CountAsync(a => a.Action == "StaffInvitationConsumed" && a.EntityId == i.UserId.ToString()));
    }
}
