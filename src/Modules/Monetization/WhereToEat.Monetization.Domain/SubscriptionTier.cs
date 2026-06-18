namespace WhereToEat.Monetization.Domain;

/// <summary>
/// The Verified subscription tier a venue holds (§7.1). Future-facing: the tiers gate paid features
/// (real photos, owner-managed menu, deeper analytics) but <b>never</b> affect organic ranking
/// (invariant #10). Behavior is fleshed out in Step 14; this is the domain shape.
/// </summary>
public enum SubscriptionTier
{
    /// <summary>Not subscribed — the venue is still fully in the catalog for free (freemium baseline).</summary>
    None = 0,

    /// <summary>Basic Verified: the partner badge + venue-managed card.</summary>
    Basic = 1,

    /// <summary>Pro Verified: Basic plus real photos, owner menu, base analytics.</summary>
    Pro = 2,
}
