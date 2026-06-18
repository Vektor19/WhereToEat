using WhereToEat.SharedKernel.ValueObjects;

namespace WhereToEat.Recommendation.Domain;

/// <summary>
/// The output of Step 2 (per-restaurant aggregation): a candidate that qualified under the match
/// predicate, carrying the representative figures the ranking step consumes — the basket price, the
/// smoothed quality (+ count), the exact <see cref="Haversine"/> distance, and the coverage (how
/// many of the selected items are present). These are the inputs to the <see cref="Scoring"/>
/// functions and the sort strategies; nothing here is recomputed downstream.
/// </summary>
public sealed record AggregatedCandidate
{
    /// <summary>Creates an aggregated candidate from the Step 2 figures.</summary>
    public AggregatedCandidate(
        Guid restaurantId,
        string name,
        Money basketPrice,
        double? smoothedRating,
        int ratingCount,
        double? distanceKm,
        int coverage)
    {
        if (restaurantId == Guid.Empty)
        {
            throw new ArgumentException("An aggregated candidate requires a non-empty restaurant id.", nameof(restaurantId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(basketPrice);
        if (ratingCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ratingCount), ratingCount, "Rating count cannot be negative.");
        }

        if (coverage < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(coverage), coverage, "Coverage cannot be negative.");
        }

        RestaurantId = restaurantId;
        Name = name;
        BasketPrice = basketPrice;
        SmoothedRating = smoothedRating;
        RatingCount = ratingCount;
        DistanceKm = distanceKm;
        Coverage = coverage;
    }

    /// <summary>The venue id.</summary>
    public Guid RestaurantId { get; }

    /// <summary>The venue display name.</summary>
    public string Name { get; }

    /// <summary>The representative basket price (cheapest matched for OR, the combo sum for AND).</summary>
    public Money BasketPrice { get; }

    /// <summary>The venue's precomputed Bayesian-smoothed rating, or <c>null</c> when none exists.</summary>
    public double? SmoothedRating { get; }

    /// <summary>The number of ratings behind <see cref="SmoothedRating"/>.</summary>
    public int RatingCount { get; }

    /// <summary>
    /// The exact Haversine distance from the user in km, or <c>null</c> when distance is disabled
    /// (the user omitted their location) or the venue is not geocoded.
    /// </summary>
    public double? DistanceKm { get; }

    /// <summary>How many of the user's selected items are present at this venue (the coverage signal).</summary>
    public int Coverage { get; }
}
