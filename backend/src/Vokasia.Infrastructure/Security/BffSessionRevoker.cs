using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Vokasia.Infrastructure.Security;

public interface IBffSessionRevoker
{
    Task RevokeUserSessionsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task RevokeUserSessionsAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default);
}

/// <summary>
/// Removes all opaque BFF sessions for deactivated identities. Redis is deliberately best-effort:
/// the database state remains authoritative, and the token endpoint re-checks IsActive on refresh.
/// This closes the normal logout window immediately without making deactivation fail just because
/// the session cache is temporarily unavailable.
/// </summary>
public sealed class BffSessionRevoker(
    IConnectionMultiplexer redis,
    ILogger<BffSessionRevoker> logger) : IBffSessionRevoker
{
    private const int UserBatchSize = 64;

    public async Task RevokeUserSessionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var database = redis.GetDatabase();
            var indexKey = BffSessionKeyPolicy.UserSessionsKey(userId);
            var sessionIds = await database.SetMembersAsync(indexKey);

            foreach (var sessionId in sessionIds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (sessionId.IsNullOrEmpty)
                {
                    continue;
                }

                await database.KeyDeleteAsync(BffSessionKeyPolicy.SessionKey(sessionId.ToString()));
            }

            // Delete the index even when it is empty so stale membership cannot accumulate.
            await database.KeyDeleteAsync(indexKey);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Gagal mencabut sesi BFF Redis untuk user {UserId}; IsActive tetap menjadi sumber kebenaran.", userId);
        }
    }

    public async Task RevokeUserSessionsAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default)
    {
        foreach (var batch in userIds.Distinct().Chunk(UserBatchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.WhenAll(batch.Select(userId => RevokeUserSessionsAsync(userId, cancellationToken)));
        }
    }
}
