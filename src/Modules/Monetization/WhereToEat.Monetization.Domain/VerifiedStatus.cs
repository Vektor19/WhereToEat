using WhereToEat.Monetization.Domain.Identifiers;
using WhereToEat.SharedKernel.Primitives;
using WhereToEat.SharedKernel.Results;

namespace WhereToEat.Monetization.Domain;

/// <summary>
/// A venue's Verified subscription status (§7.1) — a partner/confirmed status, <b>not</b> a quality
/// mark, and with <b>no effect on organic ranking</b> (invariant #10). Verified unlocks paid features
/// (most importantly the Step 5 real-photo permission gate) and carries the <see cref="SubscriptionTier"/>;
/// the always-free contact links and the organic score are untouched by it. Behavior (grant/revoke
/// use-cases, the payment seam) is fleshed out in Step 14; this is the domain shape.
/// </summary>
public sealed class VerifiedStatus : AggregateRoot<VenueRef>
{
    private VerifiedStatus(VenueRef venue, SubscriptionTier tier)
        : base(venue)
    {
        Tier = tier;
    }

    /// <summary>The venue this status is about (the aggregate's identity).</summary>
    public VenueRef Venue => Id;

    /// <summary>The current tier. <see cref="SubscriptionTier.None"/> means not Verified.</summary>
    public SubscriptionTier Tier { get; private set; }

    /// <summary>True when the venue holds any paid Verified tier (Basic or Pro).</summary>
    public bool IsVerified => Tier != SubscriptionTier.None;

    /// <summary>
    /// True when the venue is permitted to show real photos — gated by Verified status (any paid
    /// tier). This is the toggle the Step 5 <c>Photo</c> permission gate keys off; it has no bearing
    /// on organic ranking (invariant #10).
    /// </summary>
    public bool CanManageRealPhotos => IsVerified;

    /// <summary>Creates an unverified status for a venue (the freemium baseline — still in the catalog).</summary>
    public static Result<VerifiedStatus> Unverified(VenueRef venue) => Create(venue, SubscriptionTier.None);

    /// <summary>
    /// Creates a status with an explicit tier (for rehydration/seeding), rejecting a default venue
    /// reference.
    /// </summary>
    public static Result<VerifiedStatus> Create(VenueRef venue, SubscriptionTier tier)
    {
        if (venue == default)
        {
            return Result.Failure<VerifiedStatus>(
                Error.Validation("VerifiedStatus.VenueRequired", "A Verified status must reference a venue."));
        }

        return Result.Success(new VerifiedStatus(venue, tier));
    }

    /// <summary>Grants/upgrades the venue to a paid tier (Step 14 use-case sets this after payment).</summary>
    public Result Grant(SubscriptionTier tier)
    {
        if (tier == SubscriptionTier.None)
        {
            return Result.Failure(
                Error.Validation("VerifiedStatus.GrantRequiresPaidTier", "Granting Verified requires a paid tier (Basic or Pro)."));
        }

        Tier = tier;
        return Result.Success();
    }

    /// <summary>Revokes Verified (back to the freemium baseline) — the venue stays in the catalog.</summary>
    public void Revoke() => Tier = SubscriptionTier.None;
}
