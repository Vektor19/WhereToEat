using WhereToEat.Recommendation.Domain.Scoring;

namespace WhereToEat.Recommendation.Domain.Sorting;

/// <summary>
/// The <b>explicit-sort</b> ranking (CLAUDE.md §6 Step 3 (A), invariant #5 — the critical one): the
/// user's chosen field (<c>price</c> / <c>distance</c> / <c>rating</c>) is the <b>primary</b> sort
/// key, with <b>no</b> score blending. So sorting by price puts the cheapest venue first even if it
/// has only 1-of-3 selected items; coverage is applied <b>only as a tie-breaker</b> between venues
/// that are <b>equal on the primary key</b> (e.g. two equally-priced pizzerias — the 3-of-3 one
/// ranks above the 1-of-3 one). Restaurant id is the final tie-breaker for deterministic ordering.
/// <para>
/// Each of the three explicit modes is one instance of this class differing only by its primary-key
/// selector and direction — the strategy is identical, which is exactly why the invariant holds the
/// same way for all three. Venues missing the primary value (e.g. no distance/rating) sort last on
/// the primary key but still participate.
/// </para>
/// </summary>
public sealed class ExplicitSortStrategy : ISortStrategy
{
    /// <summary>Sort key for the cheapest-first explicit sort.</summary>
    public const string PriceKey = "price";

    /// <summary>Sort key for the nearest-first explicit sort.</summary>
    public const string DistanceKey = "distance";

    /// <summary>Sort key for the highest-rated-first explicit sort.</summary>
    public const string RatingKey = "rating";

    private readonly Func<AggregatedCandidate, double> _primaryKeySelector;
    private readonly bool _ascending;

    private ExplicitSortStrategy(
        string key,
        Func<AggregatedCandidate, double> primaryKeySelector,
        bool ascending)
    {
        Key = key;
        _primaryKeySelector = primaryKeySelector;
        _ascending = ascending;
    }

    /// <inheritdoc />
    public string Key { get; }

    /// <summary>
    /// Cheapest-first: the basket price ascends; the absent-price case cannot occur (every
    /// qualifying candidate has a basket price). Coverage breaks equal-price ties.
    /// </summary>
    public static ExplicitSortStrategy Price() =>
        new(PriceKey, c => (double)c.BasketPrice.Amount, ascending: true);

    /// <summary>
    /// Nearest-first: the distance ascends. A venue with no known distance sorts last on the
    /// primary key (treated as +infinity) but is not excluded.
    /// </summary>
    public static ExplicitSortStrategy Distance() =>
        new(DistanceKey, c => c.DistanceKm ?? double.PositiveInfinity, ascending: true);

    /// <summary>
    /// Highest-rated-first: the smoothed rating descends. A venue with no rating sorts last on the
    /// primary key (treated as the lowest possible) but is not excluded.
    /// </summary>
    public static ExplicitSortStrategy Rating() =>
        new(RatingKey, c => c.SmoothedRating ?? double.NegativeInfinity, ascending: false);

    /// <inheritdoc />
    public IReadOnlyList<AggregatedCandidate> Rank(
        IReadOnlyList<AggregatedCandidate> candidates,
        ScoringContext context)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(context);

        // Primary key first (this is the field the user asked to sort by — it dominates, invariant
        // #5). Coverage is ONLY a secondary key, so it can reorder venues that tie on the primary
        // key but can never lift a worse-on-primary venue above a better one. Restaurant id is the
        // final, stable tie-breaker.
        IOrderedEnumerable<AggregatedCandidate> ordered = _ascending
            ? candidates.OrderBy(_primaryKeySelector)
            : candidates.OrderByDescending(_primaryKeySelector);

        return ordered
            .ThenByDescending(c => c.Coverage)
            .ThenBy(c => c.RestaurantId)
            .ToList();
    }
}
