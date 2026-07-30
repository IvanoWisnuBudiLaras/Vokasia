namespace Vokasia.Infrastructure.Security;

/// <summary>
/// Redis key contract shared by the Next.js BFF session store and the backend revocation hook.
/// Keep these prefixes in lockstep with frontend/src/lib/bffSession.ts.
/// </summary>
public static class BffSessionKeyPolicy
{
    public const string SessionPrefix = "sess:";
    public const string UserSessionsPrefix = "user-sessions:";

    public static string SessionKey(string sessionId) => $"{SessionPrefix}{sessionId}";

    public static string UserSessionsKey(Guid userId) => $"{UserSessionsPrefix}{userId:D}";
}
