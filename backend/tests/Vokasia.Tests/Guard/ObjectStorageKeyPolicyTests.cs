using Vokasia.Domain.Common;

namespace Vokasia.Tests.Guard;

public sealed class ObjectStorageKeyPolicyTests
{
    [Fact]
    public void OwnedKey_RequiresExactTenantPrefixAndAllowedNamespace()
    {
        var tenantId = Guid.NewGuid();

        Assert.True(ObjectStorageKeyPolicy.IsOwnedKey(
            $"tenant/{tenantId}/journal/{Guid.NewGuid():N}.jpg",
            tenantId,
            "journal"));
        Assert.True(ObjectStorageKeyPolicy.IsOwnedKey(
            $"tenant/{tenantId}/visit-photo/{Guid.NewGuid():N}.jpg",
            tenantId,
            "visit-photo"));
        Assert.True(ObjectStorageKeyPolicy.IsOwnedKey(
            $"tenant/{tenantId}/visit-signature/{Guid.NewGuid():N}.png",
            tenantId,
            "visit-signature"));

        Assert.False(ObjectStorageKeyPolicy.IsOwnedKey(
            $"tenant/{Guid.NewGuid()}/journal/foreign.jpg",
            tenantId,
            "journal"));
        Assert.False(ObjectStorageKeyPolicy.IsOwnedKey(
            $"tenant/{tenantId}/visit-photo/foreign.jpg",
            tenantId,
            "journal"));
        Assert.False(ObjectStorageKeyPolicy.IsOwnedKey(
            $"tenant/{tenantId}/journal/../other.jpg",
            tenantId,
            "journal"));
    }
}
