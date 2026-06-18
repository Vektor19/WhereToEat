using Dapper;
using WhereToEat.BuildingBlocks.Persistence;
using WhereToEat.Monetization.Domain;
using WhereToEat.Monetization.Domain.Abstractions;
using WhereToEat.Monetization.Domain.Identifiers;

namespace WhereToEat.Monetization.Infrastructure.Persistence;

/// <summary>
/// The Dapper-backed <see cref="IAdPlacementRepository"/>: insert a newly created labeled placement and
/// load one by id. Rows are rehydrated through the domain factory (<see cref="AdPlacement.Create"/>) so
/// the always-labeled invariant and window guard apply on read as on write. No EF Core — Dapper only.
/// There is deliberately no method here the recommendation pipeline could call: ad placements are a
/// marked slot, never an organic-ranking input (invariant #10; the Recommendation module never
/// references this module — a Step 14 guard test pins that).
/// </summary>
public sealed class DapperAdPlacementRepository : IAdPlacementRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public DapperAdPlacementRepository(ISqlConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    /// <inheritdoc />
    public async Task AddAsync(AdPlacement placement, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(placement);

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        await connection.ExecuteAsync(
            new CommandDefinition(
                MonetizationSql.InsertAdPlacement,
                new
                {
                    Id = placement.Id.Value,
                    VenueId = placement.Venue.Value,
                    placement.TargetingKey,
                    placement.StartsAt,
                    placement.EndsAt,
                },
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<AdPlacement?> GetByIdAsync(AdPlacementId id, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        var row = await connection.QuerySingleOrDefaultAsync<AdPlacementRow>(
            new CommandDefinition(
                MonetizationSql.SelectAdPlacementById,
                new { Id = id.Value },
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        if (row is null)
        {
            return null;
        }

        var result = AdPlacement.Create(
            AdPlacementId.From(row.Id),
            VenueRef.From(row.VenueId),
            row.TargetingKey,
            row.StartsAt,
            row.EndsAt);
        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"Stored ad placement '{row.Id}' is invalid: {result.Error.Message}");
        }

        return result.Value;
    }
}
