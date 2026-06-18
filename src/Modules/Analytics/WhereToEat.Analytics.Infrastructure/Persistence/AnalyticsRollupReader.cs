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
                r.Impressions == 0 ? 0d : (double)r.CardOpens / r.Impressions))
            .ToList();
    }

    /// <summary>The flat aggregate row Dapper materializes from the GROUP BY query (counts only).</summary>
    private sealed class RollupRow
    {
        public Guid RestaurantId { get; init; }

        public DateTimeOffset HourUtc { get; init; }

        public long Impressions { get; init; }

        public long CardOpens { get; init; }
    }
}
