using System.Security.Cryptography;

namespace Vokasia.Domain.Common;

/// <summary>
/// V3.1 Manual Billing rules:
/// - Human-readable unique deterministic invoice numbers (e.g. VOK-2026-A1B2C3).
/// - Annual subscription period calculation (initial vs renewal).
/// - Expiration representation.
/// </summary>
public static class BillingRules
{
    private const string InvoiceAlphabet = "0123456789ABCDEF";

    /// <summary>
    /// Generates a human-readable, unique invoice number: "VOK-{year}-{6-char hex}".
    /// </summary>
    public static string GenerateInvoiceNumber(int? year = null)
    {
        var y = year ?? AppTimeZone.TodayJakarta().Year;
        Span<char> suffix = stackalloc char[6];
        Span<byte> randomBytes = stackalloc byte[6];
        RandomNumberGenerator.Fill(randomBytes);

        for (var i = 0; i < 6; i++)
        {
            suffix[i] = InvoiceAlphabet[randomBytes[i] % InvoiceAlphabet.Length];
        }

        return $"VOK-{y}-{new string(suffix)}";
    }

    /// <summary>
    /// Calculates the start and end dates for an annual subscription.
    /// - If there is an existing active subscription whose EndsAt is in the future:
    ///   the new subscription period starts from the current StartsAt and extends EndsAt by 1 year.
    /// - If the existing subscription is already expired or does not exist:
    ///   StartsAt is the approval effective date, and EndsAt is exactly 1 year later.
    /// </summary>
    public static (DateTimeOffset StartsAt, DateTimeOffset EndsAt) CalculateSubscriptionDates(
        DateTimeOffset effectiveDate,
        DateTimeOffset? currentStartsAt,
        DateTimeOffset? currentEndsAt,
        SubscriptionStatus? currentStatus)
    {
        if (currentEndsAt.HasValue &&
            currentEndsAt.Value > effectiveDate &&
            currentStatus == SubscriptionStatus.Active)
        {
            // Extension from current EndsAt
            var startsAt = currentStartsAt ?? effectiveDate;
            var endsAt = currentEndsAt.Value.AddYears(1);
            return (startsAt, endsAt);
        }

        // Fresh or expired
        return (effectiveDate, effectiveDate.AddYears(1));
    }

    /// <summary>
    /// Determines whether a subscription is currently active given the current timestamp.
    /// </summary>
    public static bool IsSubscriptionActive(
        SubscriptionStatus status,
        DateTimeOffset endsAt,
        DateTimeOffset now)
    {
        if (status != SubscriptionStatus.Active)
        {
            return false;
        }

        return now < endsAt;
    }
}
