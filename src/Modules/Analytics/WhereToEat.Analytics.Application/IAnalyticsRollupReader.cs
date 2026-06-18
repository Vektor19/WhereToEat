namespace WhereToEat.Analytics.Application;

/// <summary>
/// The <b>aggregation seam</b> future B2B dashboards (7.5) read through. By contract it returns
/// <b>aggregates only</b> — counts/ratios grouped by restaurant and hour — never per-user or per-event
/// rows (invariant #11: venues see only aggregates, never personal data). Step 11 implements one
/// minimal rollup (impressions / card-opens / CTR per restaurant per hour) to prove the path; the full
/// dashboard suite is a non-goal here. The implementation reads the append-only event store and groups
/// in SQL, so no raw row ever leaves this boundary.
/// </summary>
public interface IAnalyticsRollupReader
{
    /// <summary>
    /// Returns the impression / card-open / CTR aggregate per restaurant for the given inclusive hour
    /// range (UTC, hour-truncated). Aggregated rows only — no per-user/per-event data is exposed.
    /// </summary>
    Task<IReadOnlyList<RestaurantHourlyRollup>> GetRestaurantHourlyRollupAsync(
        DateTimeOffset fromHourUtc,
        DateTimeOffset toHourUtc,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// One aggregated row of the minimal rollup: for a restaurant in a given hour, how many times its card
/// was shown (<see cref="Impressions"/>), how many times it was opened (<see cref="CardOpens"/>), and
/// the resulting click-through ratio (<see cref="Ctr"/>). It carries <b>no</b> actor hash, no geohash,
/// and no per-event id — it is an aggregate, by shape.
/// </summary>
public sealed record RestaurantHourlyRollup(
    Guid RestaurantId,
    DateTimeOffset HourUtc,
    long Impressions,
    long CardOpens,
    double Ctr);
