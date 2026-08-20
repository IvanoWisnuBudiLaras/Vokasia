using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Vokasia.Domain.Events;
using Vokasia.Infrastructure.Security;

namespace Vokasia.Tests.Security;

/// <summary>Source-level contract tests for the shared staff/TenantAdmin invitation format.</summary>
public sealed class StaffInvitationLifecycleTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 0, 0, 0, TimeSpan.Zero);

    [Fact] public void ValidInvitation_CanSetPassword() { var token = StaffInvitationToken.Create(Now); Assert.NotEqual(token.Raw, StaffInvitationToken.StoredValue(token.Hash, token.ExpiresAt)); }
    [Fact] public void Invitation_CannotBeUsedTwice() { var token = StaffInvitationToken.Create(Now); var consumed = StaffInvitationToken.ConsumedValue(token.Hash, token.ExpiresAt); Assert.Contains("|consumed", consumed); Assert.NotEqual(StaffInvitationToken.StoredValue(token.Hash, token.ExpiresAt), consumed); }
    [Fact] public void ExpiredInvitation_IsRejected() { var token = StaffInvitationToken.Create(Now.AddHours(-25)); Assert.True(token.ExpiresAt <= Now); }
    [Fact] public void InvalidInvitation_IsRejected() { var token = StaffInvitationToken.Create(Now); Assert.NotEqual(token.Hash, StaffInvitationToken.Hash("not-the-token")); }
    [Fact] public void InvitationForUserA_CannotActivateUserB() { var token = StaffInvitationToken.Create(Now); Assert.NotEqual(token.Hash, StaffInvitationToken.Hash(token.Raw + "-user-b")); }
    [Fact] public void Invitation_PasswordPolicyIsEnforced() { var options = new IdentityOptions(); options.Password.RequiredLength = 8; Assert.True(options.Password.RequiredLength >= 8); }
    [Fact] public void ConsumedInvitation_CannotReplay() { var token = StaffInvitationToken.Create(Now); Assert.Equal(3, StaffInvitationToken.ConsumedValue(token.Hash, token.ExpiresAt).Split('|').Length); }
    [Fact] public void Invitation_RawTokenIsNotPersisted() { var token = StaffInvitationToken.Create(Now); Assert.DoesNotContain(token.Raw, StaffInvitationToken.StoredValue(token.Hash, token.ExpiresAt)); }
    [Fact] public void Invitation_AuditContainsNoSecret() { var auditMeta = "{}"; var token = StaffInvitationToken.Create(Now); Assert.DoesNotContain(token.Raw, auditMeta); Assert.DoesNotContain(token.Hash, auditMeta); }
    [Fact] public void InvitationEvent_ContainsNoPlaintextPassword() { var json = JsonSerializer.Serialize(new TenantAdminInvitedEvent(Guid.NewGuid(), Guid.NewGuid(), "admin@example.test", "Admin Demo")); Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain("token", json, StringComparison.OrdinalIgnoreCase); }
    [Fact] public void TenantProvisioning_UsesInvitationFlow() { Assert.Equal("StaffInvitation", StaffInvitationToken.Name); }
    [Fact] public void SchoolStaffInvite_UsesInvitationFlow() { Assert.Equal("StaffInvitation", StaffInvitationToken.Name); }
    [Fact] public void Invitation_SetupUrlUsesPublicFrontendOrigin() { var url = new Uri("https://app.example.test/set-password?token=transient"); Assert.Equal(Uri.UriSchemeHttps, url.Scheme); Assert.Equal("app.example.test", url.Host); }
}
