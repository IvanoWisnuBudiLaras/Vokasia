using System.Security.Cryptography;
using System.Text;

namespace Vokasia.Infrastructure.Security;

/// <summary>One bearer-token format shared by tenant provisioning and school-staff invitations.</summary>
public static class StaffInvitationToken
{
    public const string LoginProvider = "Vokasia";
    public const string Name = "StaffInvitation";
    public const int LifetimeHours = 24;

    public static (string Raw, string Hash, DateTimeOffset ExpiresAt) Create(DateTimeOffset now)
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-", StringComparison.Ordinal)
            .Replace("/", "_", StringComparison.Ordinal)
            .TrimEnd('=');
        var expiresAt = now.ToUniversalTime().AddHours(LifetimeHours);
        return (raw, Hash(raw), expiresAt);
    }

    public static string Hash(string raw) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));

    public static string StoredValue(string hash, DateTimeOffset expiresAt) => $"{hash}|{expiresAt.ToUniversalTime():O}";

    public static string ConsumedValue(string hash, DateTimeOffset expiresAt) => $"{hash}|{expiresAt.ToUniversalTime():O}|consumed";
}
