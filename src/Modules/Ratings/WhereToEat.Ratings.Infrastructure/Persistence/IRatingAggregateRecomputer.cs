namespace WhereToEat.Ratings.Infrastructure.Persistence;

/// <summary>
/// Recomputes the materialized per-restaurant <c>ratings.RatingAggregate</c> rollup from the raw
/// per-user <c>ratings.Rating</c> facts — the work the Step 12 rating-recompute job performs. The job
/// depends on this abstraction so it stays a thin scheduling shell; the SQL group-by + idempotent
/// upsert live here, in the Ratings module's Infrastructure assembly, next to the rollup it owns.
/// <para>
/// Materializes only the cumulative <c>sum</c>/<c>count</c> (invariant #6 — all-time). The smoothed
/// value the recommendation engine ranks on is then the single authoritative
/// <c>BayesianRatingSmoothing</c> over those two numbers, so the stored rollup always matches the
/// pure-domain formula. Idempotent: re-running recomputes from the same raw facts and rewrites the
/// same rows.
/// </para>
/// </summary>
public interface IRatingAggregateRecomputer
{
    /// <summary>
    /// Rebuilds every restaurant's aggregate (sum + count) from the raw ratings and upserts the
    /// rollup. Returns the number of restaurant aggregates written.
    /// </summary>
    Task<int> RecomputeAllAsync(CancellationToken cancellationToken = default);
}
