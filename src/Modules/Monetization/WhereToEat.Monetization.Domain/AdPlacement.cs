using WhereToEat.Monetization.Domain.Identifiers;
using WhereToEat.SharedKernel.Primitives;
using WhereToEat.SharedKernel.Results;

namespace WhereToEat.Monetization.Domain;

/// <summary>
/// A paid, <b>always-labeled</b> advertising slot for a venue (§7.2). Critically (invariant #10): an
/// ad placement is a <b>separate, marked slot</b> that the recommendation pipeline (Step 7) <b>never</b>
/// uses as a rank modifier — organic ranking is not for sale. The labeled-ad truth is the immutable
/// <see cref="IsLabeledAd"/> property, which is <c>true</c> for every placement by construction.
///
/// <para>
/// To make the invariant <b>verifiable on the type's public surface</b>, this aggregate deliberately
/// exposes <b>no</b> field that could feed an organic score — no rank/boost/weight/priority/position/
/// score/relevance member exists. It carries only the slot's identity, the venue, the targeting key
/// (a dish category / geo, used to decide <i>where the labeled slot shows</i>, not how organic results
/// rank), and the active window. Behavior (create/serve) is fleshed out in Step 14.
/// </para>
/// </summary>
public sealed class AdPlacement : AggregateRoot<AdPlacementId>
{
    private AdPlacement(
        AdPlacementId id,
        VenueRef venue,
        string targetingKey,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt)
        : base(id)
    {
        Venue = venue;
        TargetingKey = targetingKey;
        StartsAt = startsAt;
        EndsAt = endsAt;
    }

    /// <summary>
    /// Always <c>true</c> — an ad placement is, by construction, a labeled ad. There is no
    /// non-labeled variant; the UI must mark and visually separate it from organic results.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Deliberately an instance invariant marker (invariant #10): the labeled-ad test " +
            "asserts it on the instance's public surface, and it documents that every placement IS a labeled ad.")]
    public bool IsLabeledAd => true;

    /// <summary>The venue the labeled slot advertises.</summary>
    public VenueRef Venue { get; }

    /// <summary>
    /// The targeting key (e.g. a dish-category id + geo radius) deciding <b>where the labeled slot is
    /// shown</b>. It selects an ad slot's audience; it is <b>not</b> an organic-ranking input.
    /// </summary>
    public string TargetingKey { get; }

    /// <summary>When the placement starts being served.</summary>
    public DateTimeOffset StartsAt { get; }

    /// <summary>When the placement stops being served.</summary>
    public DateTimeOffset EndsAt { get; }

    /// <summary>
    /// Creates a labeled ad placement, rejecting a default venue, a blank targeting key, or a window
    /// that does not start before it ends.
    /// </summary>
    public static Result<AdPlacement> Create(VenueRef venue, string targetingKey, DateTimeOffset startsAt, DateTimeOffset endsAt)
        => Create(AdPlacementId.New(), venue, targetingKey, startsAt, endsAt);

    /// <summary>Creates a labeled ad placement with an explicit id (for rehydration/seeding).</summary>
    public static Result<AdPlacement> Create(
        AdPlacementId id,
        VenueRef venue,
        string targetingKey,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt)
    {
        if (venue == default)
        {
            return Result.Failure<AdPlacement>(
                Error.Validation("AdPlacement.VenueRequired", "An ad placement must reference a venue."));
        }

        if (string.IsNullOrWhiteSpace(targetingKey))
        {
            return Result.Failure<AdPlacement>(
                Error.Validation("AdPlacement.TargetingKeyRequired", "An ad placement must carry a targeting key."));
        }

        if (endsAt <= startsAt)
        {
            return Result.Failure<AdPlacement>(
                Error.Validation("AdPlacement.InvalidWindow", "An ad placement must start before it ends."));
        }

        return Result.Success(new AdPlacement(id, venue, targetingKey.Trim(), startsAt, endsAt));
    }

    /// <summary>True when the placement is being served at <paramref name="at"/> (within its window).</summary>
    public bool IsActiveAt(DateTimeOffset at) => at >= StartsAt && at < EndsAt;
}
