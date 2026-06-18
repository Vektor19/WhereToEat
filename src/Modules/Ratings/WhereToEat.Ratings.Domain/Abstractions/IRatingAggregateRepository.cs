using WhereToEat.Ratings.Domain.Identifiers;

namespace WhereToEat.Ratings.Domain.Abstractions;

/// <summary>
/// The Ratings module's persistence port for the per-restaurant <see cref="RatingAggregate"/> — the
/// <b>hot read path</b> the recommendation engine's candidate source ultimately joins (as a DTO field,
/// never a code reference into this domain). A Domain-shaped contract with no SQL leaks; the Dapper
/// adapter in <c>WhereToEat.Ratings.Infrastructure</c> implements it.
/// </summary>
public interface IRatingAggregateRepository
{
    /// <summary>
    /// Reads the cumulative rollup for a restaurant, or <c>null</c> if the venue has no aggregate row
    /// yet (no reviews recorded). Callers treat a missing aggregate as "neutral" (smoothed = m).
    /// </summary>
    Task<RatingAggregate?> GetByRestaurantAsync(RestaurantRef restaurant, CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts the materialized rollup (sum + count) for a restaurant — used by the Step 12
    /// rating-recompute job and by tests/seeds. Idempotent: a second upsert with the same values is a
    /// no-op effect on the stored value.
    /// </summary>
    Task UpsertAsync(RatingAggregate aggregate, CancellationToken cancellationToken = default);
}
