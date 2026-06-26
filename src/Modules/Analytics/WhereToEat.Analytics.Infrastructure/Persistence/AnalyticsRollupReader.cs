using Dapper;
using WhereToEat.Analytics.Application;
using WhereToEat.BuildingBlocks.Persistence;

namespace WhereToEat.Analytics.Infrastructure.Persistence;

/// <summary>
/// The <see cref="IAnalyticsRollupReader"/> implementation — the aggregation seam future B2B dashboards
/// read. It groups the append-only analytics event store by restaurant + hour <b>in SQL</b> and returns
/// counts/CTR only: by query shape no actor hash, geohash, or per-event id can leave this boundary
/// (invariant #11: venues see aggregates, never personal data). This proves the minimal rollup path;
/// the Step 12 job materializes the same aggregate into <c>analytics.RestaurantHourlyRollup</c> for the
/// full dashboards (a non-goal here).
/// </summary>
public sealed class AnalyticsRollupReader : IAnalyticsRollupReader
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public AnalyticsRollupReader(ISqlConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RestaurantHourlyRollup>> GetRestaurantHourlyRollupAsync(
        DateTimeOffset fromHourUtc,
        DateTimeOffset toHourUtc,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        var rows = await connection.QueryAsync<RollupRow>(
            new CommandDefinition(
                AnalyticsSql.SelectRestaurantHourlyRollup,
                new { FromHourUtc = fromHourUtc, ToHourUtc = toHourUtc },
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return rows
            .Select(r => new RestaurantHourlyRollup(
                r.RestaurantId,
                r.HourUtc,
                r.Impressions,
                r.CardOpens,
                Ratio(r.CardOpens, r.Impressions)))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<RestaurantTrafficSummary> GetRestaurantTrafficAsync(
        Guid restaurantId,
        DateTimeOffset fromHourUtc,
        DateTimeOffset toHourUtc,
        CancellationToken cancellationToken = default)
    {
        var counts = await QueryStageCountsAsync(restaurantId, fromHourUtc, toHourUtc, cancellationToken)
            .ConfigureAwait(false);

        return new RestaurantTrafficSummary(
            restaurantId,
            counts.Impressions,
            counts.CardOpens,
            counts.Actions,
            Ratio(counts.CardOpens, counts.Impressions));
    }

    /// <inheritdoc />
    public async Task<DemandBreakdown> GetDemandBreakdownAsync(
        DateTimeOffset fromHourUtc,
        DateTimeOffset toHourUtc,
        int top,
        CancellationToken cancellationToken = default)
    {
        // A non-positive cap would make TOP (@Top) invalid SQL; treat it as "no rows requested".
        if (top <= 0)
        {
            return new DemandBreakdown([], []);
        }

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        var parameters = new { FromHourUtc = fromHourUtc, ToHourUtc = toHourUtc, Top = top };

        var categories = await connection.QueryAsync<DemandRow>(
            new CommandDefinition(AnalyticsSql.SelectDemandByCategory, parameters, cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        var dishes = await connection.QueryAsync<DemandRow>(
            new CommandDefinition(AnalyticsSql.SelectDemandByDish, parameters, cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return new DemandBreakdown(
            categories.Select(r => new DemandCount(r.SelectionId, r.SearchCount)).ToList(),
            dishes.Select(r => new DemandCount(r.SelectionId, r.SearchCount)).ToList());
    }

    /// <inheritdoc />
    public async Task<PricePositioning> GetPricePositioningAsync(
        Guid restaurantId,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        var rows = await connection.QueryAsync<PricePositionRow>(
            new CommandDefinition(
                AnalyticsSql.SelectPricePositioning,
                new { RestaurantId = restaurantId },
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        var dishes = rows
            .Select(r => new DishPricePosition(
                r.DishId,
                r.VenuePrice,
                r.MedianPrice,
                r.MedianPrice is null ? null : r.VenuePrice - r.MedianPrice.Value,
                r.Currency))
            .ToList();

        return new PricePositioning(restaurantId, dishes);
    }

    /// <inheritdoc />
    public async Task<RatingsDistribution> GetRatingsDistributionAsync(
        Guid restaurantId,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        var row = await connection.QuerySingleOrDefaultAsync<RatingsRow>(
            new CommandDefinition(
                AnalyticsSql.SelectRatingsDistribution,
                new { RestaurantId = restaurantId },
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        // No aggregate row materialized yet means the venue has no ratings — a zeroed distribution.
        var count = row?.ScoreCount ?? 0;
        var sum = row?.ScoreSum ?? 0d;

        return new RatingsDistribution(
            restaurantId,
            count,
            sum,
            count == 0 ? 0d : sum / count);
    }

    /// <inheritdoc />
    public async Task<ConversionFunnel> GetConversionFunnelAsync(
        Guid restaurantId,
        DateTimeOffset fromHourUtc,
        DateTimeOffset toHourUtc,
        CancellationToken cancellationToken = default)
    {
        var counts = await QueryStageCountsAsync(restaurantId, fromHourUtc, toHourUtc, cancellationToken)
            .ConfigureAwait(false);

        return new ConversionFunnel(
            restaurantId,
            counts.Impressions,
            counts.CardOpens,
            counts.Actions,
            Ratio(counts.CardOpens, counts.Impressions),
            Ratio(counts.Actions, counts.CardOpens));
    }

    /// <summary>
    /// The shared impression/card-open/action stage counts for one restaurant over the window. Both the
    /// traffic summary and the conversion funnel are projections of these three grouped counts, so the
    /// single SQL query is reused (DRY) — and a single round-trip carries only counts across the boundary.
    /// </summary>
    private async Task<StageCounts> QueryStageCountsAsync(
        Guid restaurantId,
        DateTimeOffset fromHourUtc,
        DateTimeOffset toHourUtc,
        CancellationToken cancellationToken)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        // SelectRestaurantTraffic is a scalar aggregate with no GROUP BY: it always returns exactly one
        // row, and the SQL COALESCE-zeroes the SUMs on an empty match set — so the result is a guaranteed
        // single StageCounts with non-null counts (QuerySingle, not QuerySingleOrDefault).
        return await connection.QuerySingleAsync<StageCounts>(
            new CommandDefinition(
                AnalyticsSql.SelectRestaurantTraffic,
                new { RestaurantId = restaurantId, FromHourUtc = fromHourUtc, ToHourUtc = toHourUtc },
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    /// <summary>A safe ratio that yields 0 (not NaN) when the denominator is 0 — an empty funnel stage.</summary>
    private static double Ratio(long numerator, long denominator)
        => denominator == 0 ? 0d : (double)numerator / denominator;

    /// <summary>The flat aggregate row Dapper materializes from the GROUP BY query (counts only).</summary>
    private sealed class RollupRow
    {
        public Guid RestaurantId { get; init; }

        public DateTimeOffset HourUtc { get; init; }

        public long Impressions { get; init; }

        public long CardOpens { get; init; }
    }

    /// <summary>The three funnel stage counts for one restaurant + window (counts only — no identity).</summary>
    private sealed class StageCounts
    {
        public long Impressions { get; init; }

        public long CardOpens { get; init; }

        public long Actions { get; init; }
    }

    /// <summary>One grouped demand row: a taxonomy selection id and how many searches referenced it.</summary>
    private sealed class DemandRow
    {
        public Guid SelectionId { get; init; }

        public long SearchCount { get; init; }
    }

    /// <summary>One dish's venue price + the materialized market median (median null when absent).</summary>
    private sealed class PricePositionRow
    {
        public Guid DishId { get; init; }

        public decimal VenuePrice { get; init; }

        public decimal? MedianPrice { get; init; }

        public string Currency { get; init; } = string.Empty;
    }

    /// <summary>The cumulative all-time ratings rollup row (count + score sum) — aggregate only.</summary>
    private sealed class RatingsRow
    {
        public long ScoreCount { get; init; }

        public double ScoreSum { get; init; }
    }
}
