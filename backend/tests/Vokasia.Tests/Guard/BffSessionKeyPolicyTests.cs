using Vokasia.Infrastructure.Security;

namespace Vokasia.Tests.Guard;

public sealed class BffSessionKeyPolicyTests
{
    [Fact]
    public void UsesTheSameRedisKeyContractAsTheNextBff()
    {
        var userId = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");

        Assert.Equal("sess:session-123", BffSessionKeyPolicy.SessionKey("session-123"));
        Assert.Equal("user-sessions:01234567-89ab-cdef-0123-456789abcdef", BffSessionKeyPolicy.UserSessionsKey(userId));
    }
}
