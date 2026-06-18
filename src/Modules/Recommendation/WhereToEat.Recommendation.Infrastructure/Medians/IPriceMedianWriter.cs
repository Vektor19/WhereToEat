namespace WhereToEat.Recommendation.Infrastructure.Medians;

/// <summary>
/// The write half of the price-median feature: recomputes and persists the Step 7
/// <c>recommendation.PriceMedian</c> table the DB-only <see cref="DbPriceMedianProvider"/> reads. The
/// Step 12 worker's nightly job depends on this abstraction so the job stays a thin scheduling shell
/// over the (infrastructure-owned) calculation + persistence. Kept in the Recommendation module's
/// Infrastructure assembly next to the table it writes; the worker host references that assembly for
/// composition (a host may reference module Infrastructure).
/// </summary>
public interface IPriceMedianWriter
{
    /// <summary>
    /// Recomputes every per-area / per-category median (city-wide fallback below
    /// <paramref name="areaSampleThreshold"/>) from the current menu prices and replaces the table
    /// contents transactionally. Returns the number of median rows written. Idempotent.
    /// </summary>
    Task<int> RecomputeAsync(int areaSampleThreshold, CancellationToken cancellationToken = default);
}
