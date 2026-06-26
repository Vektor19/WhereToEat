using Dapper;
using WhereToEat.BuildingBlocks.Persistence;
using WhereToEat.Ratings.Domain;
using WhereToEat.Ratings.Domain.Abstractions;
using WhereToEat.Ratings.Domain.Identifiers;

namespace WhereToEat.Ratings.Infrastructure.Persistence;

/// <summary>
/// The Dapper-backed <see cref="IRatingRepository"/>: the write side for the raw per-user
/// <c>ratings.Rating</c> fact (Step 21). A keyed lookup decides revise-vs-create, then a single
/// parameterised insert or update persists the row — the unique <c>UQ_Rating_Restaurant_User</c>
/// constraint guarantees one row per user+restaurant, so a revise overwrites the same row and never
/// duplicates (invariant #6). Loaded rows are rehydrated through the domain factory
/// (<see cref="Rating.Create(RatingId, RestaurantRef, UserRef, int, DateTimeOffset)"/>) so the same
/// invariants apply on read as on write. No EF Core — Dapper only (EF is Admin.Infrastructure-only).
/// </summary>
public sealed class DapperRatingRepository : IRatingRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public DapperRatingRepository(ISqlConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    /// <inheritdoc />
    public async Task<Rating?> GetByRestaurantAndUserAsync(
        RestaurantRef restaurant,
        UserRef user,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        var row = await connection.QuerySingleOrDefaultAsync<RatingRow>(
            new CommandDefinition(
                RatingsSql.SelectRatingByRestaurantAndUser,
                new { RestaurantId = restaurant.Value, UserId = user.Value },
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        if (row is null)
        {
            return null;
        }

        var result = Rating.Create(
            RatingId.From(row.Id),
            RestaurantRef.From(row.RestaurantId),
            UserRef.From(row.UserId),
            row.Score,
            row.GivenAt);

        if (result.IsFailure)
        {
            // A stored row that violates the domain invariants is a data-integrity bug, not
            // recoverable input — surface it loudly rather than returning a silently-wrong rating.
            throw new InvalidOperationException(
                $"Stored rating '{row.Id}' is invalid: {result.Error.Message}");
        }

        return result.Value;
    }

    /// <inheritdoc />
    public async Task AddAsync(Rating rating, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rating);

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        await connection.ExecuteAsync(
            new CommandDefinition(
                RatingsSql.InsertRating,
                new
                {
                    Id = rating.Id.Value,
                    RestaurantId = rating.Restaurant.Value,
                    UserId = rating.User.Value,
                    rating.Score,
                    rating.GivenAt,
                },
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Rating rating, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rating);

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        await connection.ExecuteAsync(
            new CommandDefinition(
                RatingsSql.UpdateRating,
                new
                {
                    Id = rating.Id.Value,
                    rating.Score,
                    rating.GivenAt,
                },
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }
}
