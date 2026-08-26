using Vokasia.Domain.Common;

namespace Vokasia.Domain.Entities;

/// <summary>
/// Paket langganan GLOBAL (bukan tenant-scoped) — FR-SA-03.
/// V3.1 Manual Billing MVP: period ANNUAL only. PriceMonthly dibiarkan sebagai legacy field
/// (tetap dipakai BillingCronJobs untuk kompatibilitas V2/V3, TIDAK diekspos ke UI V3.1).
/// </summary>
public class Plan
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public decimal PriceMonthly { get; set; }
    public decimal PriceAnnual { get; set; }
    public int MaxStudents { get; set; }
    public int MaxPlacements { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Feature flag per plan ATAU override per tenant (FR-SA-03). Salah satu dari PlanId/TenantId diisi.
/// Resolusi efektif: GetEffectiveFlags (H6-E1) — override tenant menang atas plan.
/// </summary>
public class FeatureFlag
{
    public Guid Id { get; set; }
    public Guid? PlanId { get; set; }
    public Guid? TenantId { get; set; }
    public string Key { get; set; } = default!;
    public bool Enabled { get; set; }
}

/// <summary>
/// V3.1 Manual Billing — Invoice. Snapshot invariant: Amount, PlanName, PlanId, StudentCapacity,
/// dan Periode (Annual) di-SNAPSHOT saat invoice diterbitkan sehingga perubahan Plan
/// setelahnya tidak mengubah nilai historis invoice. InvoiceNumber adalah human-readable,
/// server-side generated, dan UNIQUE di database.
/// </summary>
public class Invoice : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    /// <summary>Human-readable unique number, server-side generated, e.g. VOK-2026-000123.</summary>
    public string InvoiceNumber { get; set; } = default!;

    /// <summary>Snapshot field — Plan identity at issuance time.</summary>
    public Guid PlanId { get; set; }

    /// <summary>Snapshot field — Plan display name at issuance time.</summary>
    public string PlanNameSnapshot { get; set; } = default!;

    /// <summary>Snapshot field — billed amount at issuance time (annual).</summary>
    public decimal AmountSnapshot { get; set; }

    /// <summary>Convenience alias for AmountSnapshot.</summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public decimal Amount
    {
        get => AmountSnapshot;
        set => AmountSnapshot = value;
    }

    /// <summary>Snapshot field — plan capacity at issuance time.</summary>
    public int StudentCapacitySnapshot { get; set; }

    /// <summary>Annual period — server-side "tanggal 1 bulan mulai" (e.g. 2026-01-01).</summary>
    public DateOnly PeriodMonth { get; set; }

    public DateTimeOffset IssuedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset DueAt { get; set; }
    public DateTimeOffset? PaidAt { get; set; }

    public Common.InvoiceStatus Status { get; set; } = Common.InvoiceStatus.Unpaid;

    public string? ProofKey { get; set; }

    /// <summary>Wajib diisi ketika Status == Rejected.</summary>
    public string? RejectionReason { get; set; }
}

/// <summary>
/// V3.1 Manual Billing — PaymentSubmission. Append-only history of payment attempts.
/// A new submission can be made after Rejected; previous verification audit is preserved.
/// </summary>
public class PaymentSubmission : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid InvoiceId { get; set; }
    public Guid SubmittedBy { get; set; }
    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;
    public string ProofKey { get; set; } = default!;
    public string? Note { get; set; }

    /// <summary>Verification outcome: null = pending, true = approved, false = rejected.</summary>
    public bool? Approved { get; set; }

    public Guid? VerifiedBy { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }
    public string? VerificationReason { get; set; }
}

/// <summary>
/// V3.1 Manual Billing — Subscription. Each tenant has at most one active/pending subscription.
/// Source of truth for service access state.
/// </summary>
public class Subscription : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid PlanId { get; set; }
    public string PlanNameSnapshot { get; set; } = default!;
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public Common.SubscriptionStatus Status { get; set; } = Common.SubscriptionStatus.Pending;
    public Guid SourceInvoiceId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
