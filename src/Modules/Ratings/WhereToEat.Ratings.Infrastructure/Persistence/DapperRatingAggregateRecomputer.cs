using Dapper;
using WhereToEat.BuildingBlocks.Persistence;
using WhereToEat.Ratings.Domain;
using WhereToEat.Ratings.Domain.Abstractions;
using WhereToEat.Ratings.Domain.Identifiers;

namespace WhereToEat.Ratings.Infrastructure.Persistence;

/// <summary>
/// The Dapper-backed <see cref="IRatingAggregateRecomputer"/>: it reads the cumulative sum/count per
/// restaurant straight from the raw <c>ratings.Rating</c> facts, rehydrates each through the domain
/// factory (so the same invariants hold), and upserts the materialized rollup via the existing
/// <see cref="IRatingAggregateRepository"/> <c>MERGE</c>. No EF Core — Dapper only.
/// </summary>
public sealed class DapperRatingAggregateRecomputer : IRatingAggregateRecomputer
{
    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly IRatingAggregateRepository _repository;

    public DapperRatingAggregateRecomputer(
        ISqlConnectionFactory connectionFactory,
        IRatingAggregateRepository repository)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentNullException.ThrowIfNull(repository);
        _connectionFactory = connectionFactory;
        _repository = repository;
    }

    /// <inheritdoc />
    public async Task<int> RecomputeAllAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        var rawAggregates = (await connection.QueryAsync<RawAggregateRow>(
                new CommandDefinition(
                    RatingsSql.SelectRawAggregatesGroupedByRestaurant,
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false))
            .ToList();

        var written = 0;
        foreach (var raw in rawAggregates)
        {
            var aggregateResult = RatingAggregate.Create(
                RestaurantRef.From(raw.RestaurantId),
                raw.ScoreSum,
                raw.ScoreCount);

            if (aggregateResult.IsFailure)
            {
                // A raw group that cannot form a valid aggregate is a data-integrity bug, not
                // recoverable input — surface it rather than silently materializing a wrong rollup.
                throw new InvalidOperationException(
                    $"Recomputed rating aggregate for restaurant '{raw.RestaurantId}' is invalid: {aggregateResult.Error.Message}");
            }

            await _repository.UpsertAsync(aggregateResult.Value, cancellationToken).ConfigureAwait(false);
            written++;
        }

        return written;
    }

    private sealed record RawAggregateRow
    {
        public Guid RestaurantId { get; init; }
        public double ScoreSum { get; init; }
        public long ScoreCount { get; init; }
    }
}
