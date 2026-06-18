using System.Data;
using Dapper;
using WhereToEat.BuildingBlocks.Persistence;
using WhereToEat.Parsing.Application;
using WhereToEat.Parsing.Domain;

namespace WhereToEat.Parsing.Infrastructure.Persistence;

/// <summary>
/// The Dapper-backed <see cref="IParseQuarantineStore"/> (invariant #2 — unmappable dishes are
/// quarantined, never dropped): it appends the normalizer's unmappable raw dish lines to the
/// <c>parsing.ParseQuarantine</c> review queue for later admin resolution. A quarantined item is never
/// written as a live <c>catalog.MenuItem</c> until an admin maps it. The whole batch is inserted under
/// one connection/transaction so a partial write cannot leave the queue inconsistent. No EF Core —
/// Dapper only (EF is Admin.Infrastructure-only).
/// </summary>
public sealed class DapperParseQuarantineStore : IParseQuarantineStore
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public DapperParseQuarantineStore(ISqlConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    /// <inheritdoc />
    public async Task AddAsync(IReadOnlyList<ParseQuarantineItem> items, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0)
        {
            return;
        }

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);

        foreach (var item in items)
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    ParseQuarantineSql.Insert,
                    new
                    {
                        item.Id,
                        item.RestaurantName,
                        item.RestaurantAddressLine,
                        item.RawName,
                        PriceAmount = item.Price.Amount,
                        PriceCurrency = item.Price.Currency,
                        item.Weight,
                        item.CategoryHint,
                        item.DetectedAtUtc,
                    },
                    transaction,
                    cancellationToken: cancellationToken))
                .ConfigureAwait(false);
        }

        transaction.Commit();
    }
}
