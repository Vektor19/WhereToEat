using WhereToEat.SharedKernel.ValueObjects;

namespace WhereToEat.Recommendation.Domain;

/// <summary>
/// A restaurant that survived the match predicate (Step 1) carried into the engine together with
/// the raw per-restaurant facts the aggregation step (Step 2) needs: the menu items that matched
/// the selection (each priced in <see cref="Money"/>), the venue's <b>already-smoothed</b> all-time
/// rating + review count, and the venue's location. The smoothed rating is a <b>plain value the
/// engine consumes</b> — it is computed by the Ratings module and joined in by the infrastructure
/// candidate source, so the engine never references <c>Ratings.Domain</c> (invariant boundary).
/// <para>
/// This is a pure value the Domain receives; it carries no I/O and no Ratings domain types. The
/// <see cref="MatchedItems"/> are the selected items (by id) actually present at the venue, with
/// the price of each — basket price and coverage are derived from this list during aggregation.
/// </para>
/// </summary>
public sealed class RestaurantCandidate
{
    /// <summary>
    /// Creates a candidate. <paramref name="matchedItems"/> is the subset of the user's selection
    /// actually present at this venue (deduplicated by selection id by the candidate source).
    /// </summary>
    /// <param name="restaurantId">The venue id.</param>
    /// <param name="name">The venue display name.</param>
    /// <param name="matchedItems">The matched menu offerings (one per matched selected item).</param>
    /// <param name="smoothedRating">
    /// The venue's Bayesian-smoothed all-time rating (the value <c>f_quality</c> consumes), or
    /// <c>null</c> when the venue has no materialized rating aggregate yet.
    /// </param>
    /// <param name="ratingCount">The number of ratings behind the smoothed value (0 when none).</param>
    /// <param name="distanceKm">
    /// The exact distance (km) from the user to the venue, computed locally by the candidate source
    /// via <c>Haversine</c> (invariant #7 — the SQL spatial index narrows, Haversine decides). It is
    /// <c>null</c> when distance is not applicable (the user omitted geo, or the venue is not
    /// geocoded). The engine consumes this precomputed value and never re-derives it.
    /// </param>
    public RestaurantCandidate(
        Guid restaurantId,
        string name,
        IReadOnlyList<MatchedItem> matchedItems,
        double? smoothedRating,
        int ratingCount,
        double? distanceKm)
    {
        if (restaurantId == Guid.Empty)
        {
            throw new ArgumentException("A candidate requires a non-empty restaurant id.", nameof(restaurantId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(matchedItems);
        if (ratingCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ratingCount), ratingCount, "Rating count cannot be negative.");
        }

        RestaurantId = restaurantId;
        Name = name;
        MatchedItems = matchedItems;
        SmoothedRating = smoothedRating;
        RatingCount = ratingCount;
        DistanceKm = distanceKm;
    }

    /// <summary>The venue id.</summary>
    public Guid RestaurantId { get; }

    /// <summary>The venue display name.</summary>
    public string Name { get; }

    /// <summary>
    /// The user's selected items actually present at this venue. Its <c>Count</c> is the venue's
    /// <b>coverage</b>; the prices drive the basket price (cheapest for OR, sum for AND).
    /// </summary>
    public IReadOnlyList<MatchedItem> MatchedItems { get; }

    /// <summary>
    /// The venue's Bayesian-smoothed all-time rating (the precomputed value <c>f_quality</c> reads),
    /// or <c>null</c> when no rating aggregate exists. The engine never recomputes this.
    /// </summary>
    public double? SmoothedRating { get; }

    /// <summary>The number of ratings behind <see cref="SmoothedRating"/>.</summary>
    public int RatingCount { get; }

    /// <summary>
    /// The exact distance (km) from the user, precomputed locally via <c>Haversine</c> (invariant
    /// #7), or <c>null</c> when distance is not applicable. The engine never re-derives it.
    /// </summary>
    public double? DistanceKm { get; }
}

/// <summary>
/// One of the user's selected items found at a venue: which selection it satisfied and the price
/// of the cheapest menu offering for it at that venue. The basket price and coverage are derived
/// from the set of matched items during aggregation (Step 2).
/// </summary>
public sealed record MatchedItem(SelectionKey Selection, Money Price);

/// <summary>
/// A selection the user made — either a whole category or a specific dish (the two-level taxonomy,
/// invariant #2). <b>Exactly one</b> id is populated; the factories are the only construction path,
/// so the "neither / both" invalid states are unreachable. Used as the predicate input for match
/// strategies and to identify which selection a <see cref="MatchedItem"/> satisfied (for coverage).
/// </summary>
public sealed record SelectionKey
{
    private SelectionKey(Guid? categoryId, Guid? dishId)
    {
        CategoryId = categoryId;
        DishId = dishId;
    }

    /// <summary>The selected category id, or <c>null</c> when this selection is a dish.</summary>
    public Guid? CategoryId { get; }

    /// <summary>The selected dish id, or <c>null</c> when this selection is a category.</summary>
    public Guid? DishId { get; }

    /// <summary>Selects a whole category.</summary>
    public static SelectionKey Category(Guid categoryId)
    {
        if (categoryId == Guid.Empty)
        {
            throw new ArgumentException("A category selection requires a non-empty category id.", nameof(categoryId));
        }

        return new SelectionKey(categoryId, null);
    }

    /// <summary>Selects a specific dish.</summary>
    public static SelectionKey Dish(Guid dishId)
    {
        if (dishId == Guid.Empty)
        {
            throw new ArgumentException("A dish selection requires a non-empty dish id.", nameof(dishId));
        }

        return new SelectionKey(null, dishId);
    }
}
