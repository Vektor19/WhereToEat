namespace WhereToEat.Analytics.Application;

/// <summary>
/// The <b>aggregation seam</b> the B2B dashboards (§7.5) read through. By contract it returns
/// <b>aggregates only</b> — counts/ratios grouped by restaurant, hour, or non-identifying dimension —
/// <b>never</b> per-user or per-event rows (invariant #11: venues see only aggregates, never personal
/// data). Step 11 implemented one minimal rollup (impressions / card-opens / CTR per restaurant per
/// hour) to prove the path; Step 23 extends the seam with the additional §7.5 metric families
/// (visibility/traffic with action clicks, demand by category/dish, price positioning, ratings
/// distribution, conversion funnel). Every implementation reads the append-only event store (joining
/// the price-median and ratings-aggregate materializations where needed) and groups <b>in SQL</b>, so
/// no actor hash, no per-event id, no precise coordinate, and no raw row ever leaves this boundary.
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

    /// <summary>
    /// §7.5 <b>visibility / traffic</b> for a single restaurant over the inclusive hour range: total
    /// impressions, card-opens, action clicks (Look-on-map / phone / site / social), and the resulting
    /// click-through ratio. One aggregate row collapsing the window — counts/ratios only.
    /// </summary>
    Task<RestaurantTrafficSummary> GetRestaurantTrafficAsync(
        Guid restaurantId,
        DateTimeOffset fromHourUtc,
        DateTimeOffset toHourUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// §7.5 <b>demand by category/dish</b>: how many search/selection events referenced each category and
    /// each dish in the inclusive hour range (the demand signal — what is searched in an area, even for
    /// items a venue does not carry). Grouped counts per non-identifying selection id; capped to the
    /// <paramref name="top"/> most-searched. No actor/per-event data — a selection id is not a person.
    /// </summary>
    Task<DemandBreakdown> GetDemandBreakdownAsync(
        DateTimeOffset fromHourUtc,
        DateTimeOffset toHourUtc,
        int top,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// §7.5 <b>price positioning</b> for a restaurant: the venue's per-dish menu prices against the
    /// area/category median the engine already materializes (<c>recommendation.PriceMedian</c>), so the
    /// operator sees where each dish sits relative to the market (cheaper / dearer than median). One
    /// aggregate row per dish (median + the venue's price), no behavioural/per-user data.
    /// </summary>
    Task<PricePositioning> GetPricePositioningAsync(
        Guid restaurantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// §7.5 <b>ratings distribution</b> for a restaurant: the cumulative all-time rating count and score
    /// sum from the materialized <c>ratings.RatingAggregate</c> rollup (invariant #6). Aggregate counts
    /// only — never an individual user's score or identity.
    /// </summary>
    Task<RatingsDistribution> GetRatingsDistributionAsync(
        Guid restaurantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// §7.5 <b>conversion funnel</b> for a restaurant over the inclusive hour range:
    /// impression → card-open → action, as stage counts and stage-to-stage ratios. Aggregates only —
    /// the funnel is counts of events, never a traceable per-user path.
    /// </summary>
    Task<ConversionFunnel> GetConversionFunnelAsync(
        Guid restaurantId,
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

/// <summary>
/// §7.5 visibility/traffic aggregate for one restaurant over a window: total impressions, card-opens,
/// action clicks, and the impression→card-open click-through ratio. Counts/ratios only — by shape no
/// actor hash or per-event row is present (invariant #11).
/// </summary>
public sealed record RestaurantTrafficSummary(
    Guid RestaurantId,
    long Impressions,
    long CardOpens,
    long Actions,
    double Ctr);

/// <summary>
/// §7.5 demand aggregate: the most-searched categories and dishes in a window, as grouped counts per
/// selection id. A selection id identifies a taxonomy item (invariant #2), never a person — these are
/// pure demand signals, no actor/per-event data.
/// </summary>
public sealed record DemandBreakdown(
    IReadOnlyList<DemandCount> Categories,
    IReadOnlyList<DemandCount> Dishes);

/// <summary>One demand row: how many search/selection events referenced a single category or dish id.</summary>
public sealed record DemandCount(Guid SelectionId, long SearchCount);

/// <summary>
/// §7.5 price-positioning aggregate for one restaurant: each carried dish's price against the
/// area/category median the engine materializes. No behavioural data — purely the venue's own prices
/// and the precomputed market medians.
/// </summary>
public sealed record PricePositioning(
    Guid RestaurantId,
    IReadOnlyList<DishPricePosition> Dishes);

/// <summary>
/// One dish's price position: the venue's price, the market median for that dish (null when no median
/// has been materialized for the dish), and the signed delta (venue − median; negative = cheaper than
/// market). Currency follows the venue's own menu-item currency.
/// </summary>
public sealed record DishPricePosition(
    Guid DishId,
    decimal VenuePrice,
    decimal? MedianPrice,
    decimal? DeltaFromMedian,
    string Currency);

/// <summary>
/// §7.5 ratings-distribution aggregate for one restaurant, read from the cumulative all-time
/// <c>ratings.RatingAggregate</c> rollup (invariant #6): the number of distinct ratings, their score
/// sum, and the resulting raw average (0 when there are no ratings). Aggregate-only — never a single
/// user's score.
/// </summary>
public sealed record RatingsDistribution(
    Guid RestaurantId,
    long RatingCount,
    double ScoreSum,
    double AverageScore);

/// <summary>
/// §7.5 conversion-funnel aggregate for one restaurant over a window: the impression → card-open →
/// action stage counts and the two stage-to-stage ratios (open rate, action rate). Counts/ratios only —
/// not a traceable per-user journey (invariant #11).
/// </summary>
public sealed record ConversionFunnel(
    Guid RestaurantId,
    long Impressions,
    long CardOpens,
    long Actions,
    double ImpressionToCardOpenRate,
    double CardOpenToActionRate);
