namespace Vokasia.Domain.Common;

/// <summary>
/// Validates object keys supplied by a client before they are persisted as references.
/// Reads of legacy rows remain compatible; callers apply this policy to new writes and
/// public projections as a defense-in-depth boundary.
/// </summary>
public static class ObjectStorageKeyPolicy
{
    public static bool IsOwnedKey(string? objectKey, Guid tenantId, string namespaceName)
    {
        if (string.IsNullOrWhiteSpace(objectKey) || string.IsNullOrWhiteSpace(namespaceName))
        {
            return false;
        }

        if (objectKey.Contains('\\') || objectKey.StartsWith("/", StringComparison.Ordinal))
        {
            return false;
        }

        var segments = objectKey.Split('/');
        if (segments.Any(segment => segment.Length == 0 || segment is "." or ".."))
        {
            return false;
        }

        var prefix = $"tenant/{tenantId:D}/{namespaceName}/";
        return objectKey.StartsWith(prefix, StringComparison.Ordinal) && objectKey.Length > prefix.Length;
    }
}
