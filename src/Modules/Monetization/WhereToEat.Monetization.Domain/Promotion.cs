using WhereToEat.Monetization.Domain.Identifiers;
using WhereToEat.SharedKernel.Primitives;
using WhereToEat.SharedKernel.Results;

namespace WhereToEat.Monetization.Domain;

/// <summary>
/// A venue-created promotion/coupon (§7.4) — e.g. "−20% on a dish". Like <see cref="AdPlacement"/> it
/// is an <b>always-labeled</b>, paid marketing surface shown <b>separately</b> from organic results,
/// and (invariant #10) it carries <b>no</b> field that feeds organic ranking — no rank/boost/weight/
/// priority/score member exists on the type's public surface. It holds only its identity, the venue,
/// the human-readable offer text, and the active window. Behavior is fleshed out in Step 14.
/// </summary>
public sealed class Promotion : AggregateRoot<PromotionId>
{
    private Promotion(PromotionId id, VenueRef venue, string offer, DateTimeOffset startsAt, DateTimeOffset endsAt)
        : base(id)
    {
        Venue = venue;
        Offer = offer;
        StartsAt = startsAt;
        EndsAt = endsAt;
    }

    /// <summary>
    /// Always <c>true</c> — a promotion is a labeled marketing surface by construction, visually
    /// separated from organic results.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Deliberately an instance invariant marker (invariant #10): the labeled-ad test " +
            "asserts it on the instance's public surface, and it documents that every promotion IS a labeled ad.")]
    public bool IsLabeledAd => true;

    /// <summary>The venue this promotion belongs to.</summary>
    public VenueRef Venue { get; }

    /// <summary>The human-readable offer text shown to users (e.g. "−20% on the Margherita").</summary>
    public string Offer { get; }

    /// <summary>When the promotion starts.</summary>
    public DateTimeOffset StartsAt { get; }

    /// <summary>When the promotion ends.</summary>
    public DateTimeOffset EndsAt { get; }

    /// <summary>
    /// Creates a promotion, rejecting a default venue, a blank offer, or a window that does not start
    /// before it ends.
    /// </summary>
    public static Result<Promotion> Create(VenueRef venue, string offer, DateTimeOffset startsAt, DateTimeOffset endsAt)
        => Create(PromotionId.New(), venue, offer, startsAt, endsAt);

    /// <summary>Creates a promotion with an explicit id (for rehydration/seeding).</summary>
    public static Result<Promotion> Create(PromotionId id, VenueRef venue, string offer, DateTimeOffset startsAt, DateTimeOffset endsAt)
    {
        if (venue == default)
        {
            return Result.Failure<Promotion>(
                Error.Validation("Promotion.VenueRequired", "A promotion must reference a venue."));
        }

        if (string.IsNullOrWhiteSpace(offer))
        {
            return Result.Failure<Promotion>(
                Error.Validation("Promotion.OfferRequired", "A promotion must carry an offer description."));
        }

        if (endsAt <= startsAt)
        {
            return Result.Failure<Promotion>(
                Error.Validation("Promotion.InvalidWindow", "A promotion must start before it ends."));
        }

        return Result.Success(new Promotion(id, venue, offer.Trim(), startsAt, endsAt));
    }

    /// <summary>True when the promotion is live at <paramref name="at"/> (within its window).</summary>
    public bool IsActiveAt(DateTimeOffset at) => at >= StartsAt && at < EndsAt;
}
