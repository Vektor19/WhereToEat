using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using WhereToEat.BuildingBlocks.Persistence;

namespace WhereToEat.Recommendation.Infrastructure.Medians;

/// <summary>
/// Populates the Step 7 <c>recommendation.PriceMedian</c> table the DB-only
/// <see cref="DbPriceMedianProvider"/> reads — the write half of the Step 12 nightly median job. It
/// reads the current menu prices (joined to each restaurant's stored coordinates and its dish's
/// category), hands them to the pure <see cref="PriceMedianCalculator"/> (per-area medians with the
/// city-wide fallback below threshold N), then <b>replaces</b> the table contents transactionally so
/// the read side always sees a complete, consistent snapshot.
/// <para>
/// DB-only — NO Redis (invariant: Step 13 adds Redis as a transparent caching decorator over the
/// provider port, not here). Idempotent: re-running recomputes from the same source data and rewrites
/// the same rows, so a missed/retried nightly run converges to the same table.
/// </para>
/// </summary>
public sealed class PriceMedianWriter : IPriceMedianWriter
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public PriceMedianWriter(ISqlConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    /// <inheritdoc />
    public async Task<int> RecomputeAsync(int areaSampleThreshold, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        // 1. Read the current priced menu items (price + dish + category + restaurant coordinates).
        var priced = (await connection.QueryAsync<PricedMenuItemRow>(
                new CommandDefinition(
                    PriceMedianSql.SelectPricedMenuItems,
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false))
            .Select(r => new PricedMenuItem(
                r.Latitude,
                r.Longitude,
                r.DishId,
                r.CategoryId,
                r.PriceAmount,
                r.PriceCurrency))
            .ToList();

        // 2. Compute the median rows (pure) — per-area above N, plus the always-present city-wide rows.
        var rows = PriceMedianCalculator.Calculate(priced, areaSampleThreshold);

        // 3. Replace the table contents in one transaction so the read side never sees a partial state.
        //    SERIALIZABLE (not the default READ COMMITTED): under RCSI a concurrent reader could observe
        //    the table between the DELETE and the INSERT-commit and see it empty — making f_price go
        //    neutral for that window. Serializable holds key-range locks so a reader sees either the old
        //    snapshot or the new one, never an empty intermediate. This is a nightly single-writer job,
        //    so the stricter isolation has no contention cost.
        var sqlConnection = (SqlConnection)connection;
        using var transaction = (SqlTransaction)await sqlConnection
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            .ConfigureAwait(false);

        await connection.ExecuteAsync(
                new CommandDefinition(
                    PriceMedianSql.DeleteAll,
                    transaction: transaction,
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        foreach (var row in rows)
        {
            await connection.ExecuteAsync(
                    new CommandDefinition(
                        PriceMedianSql.Insert,
                        new
                        {
                            row.AreaKey,
                            row.DishId,
                            row.CategoryId,
                            row.MedianAmount,
                            row.Currency,
                            row.SampleSize,
                        },
                        transaction: transaction,
                        cancellationToken: cancellationToken))
                .ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return rows.Count;
    }

    // Dapper row shape for the priced-item read; mapped into the pure PricedMenuItem above.
    private sealed record PricedMenuItemRow
    {
        public double? Latitude { get; init; }
        public double? Longitude { get; init; }
        public Guid DishId { get; init; }
        public Guid CategoryId { get; init; }
        public decimal PriceAmount { get; init; }
        public string PriceCurrency { get; init; } = string.Empty;
    }
}
