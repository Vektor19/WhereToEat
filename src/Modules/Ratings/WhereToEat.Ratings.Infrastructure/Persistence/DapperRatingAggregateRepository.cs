using Dapper;
using WhereToEat.BuildingBlocks.Persistence;
using WhereToEat.Ratings.Domain;
using WhereToEat.Ratings.Domain.Abstractions;
using WhereToEat.Ratings.Domain.Identifiers;

namespace WhereToEat.Ratings.Infrastructure.Persistence;

/// <summary>
/// The Dapper-backed <see cref="IRatingAggregateRepository"/>: a single keyed lookup for the hot read
/// path and a <c>MERGE</c>-based upsert for the Step 12 recompute job. Rows are rehydrated through the
/// domain factory (<see cref="RatingAggregate.Create"/>) so the same invariants apply on read as on
/// write; recorded domain events are cleared after rehydration so a read never re-raises them. No EF
/// Core — Dapper only (EF is Admin.Infrastructure-only).
/// </summary>
public sealed class DapperRatingAggregateRepository : IRatingAggregateRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public DapperRatingAggregateRepository(ISqlConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    /// <inheritdoc />
    public async Task<RatingAggregate?> GetByRestaurantAsync(RestaurantRef restaurant, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        var row = await connection.QuerySingleOrDefaultAsync<RatingAggregateRow>(
            new CommandDefinition(
                RatingsSql.SelectAggregateByRestaurant,
                new { RestaurantId = restaurant.Value },
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        if (row is null)
        {
            return null;
        }

        var result = RatingAggregate.Create(RestaurantRef.From(row.RestaurantId), row.ScoreSum, row.ScoreCount);
        if (result.IsFailure)
        {
            // A stored row that violates the domain invariants is a data-integrity bug, not
            // recoverable input — surface it loudly rather than returning a silently-wrong aggregate.
            throw new InvalidOperationException(
                $"Stored rating aggregate for restaurant '{row.RestaurantId}' is invalid: {result.Error.Message}");
        }

        var aggregate = result.Value;
        aggregate.ClearDomainEvents();
        return aggregate;
    }

    /// <inheritdoc />
    public async Task UpsertAsync(RatingAggregate aggregate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        await connection.ExecuteAsync(
            new CommandDefinition(
                RatingsSql.UpsertAggregate,
                new
                {
                    RestaurantId = aggregate.Restaurant.Value,
                    ScoreSum = aggregate.Sum,
                    ScoreCount = aggregate.Count,
                },
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }
}
