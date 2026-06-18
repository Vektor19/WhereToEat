using Dapper;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using WhereToEat.BuildingBlocks.Persistence;
using WhereToEat.Ratings.Domain.Identifiers;
using WhereToEat.Ratings.Infrastructure.Persistence;
using WhereToEat.SharedKernel.Ratings;
using Xunit;

namespace WhereToEat.Worker.Integration;

/// <summary>
/// The rating-recompute proof (Step 12): the <see cref="DapperRatingAggregateRecomputer"/> materializes
/// the per-restaurant aggregate (sum + count) from the raw <c>ratings.Rating</c> facts, and the smoothed
/// value over the materialized rollup equals the single authoritative SharedKernel
/// <see cref="BayesianRatingSmoothing"/> computed directly from the same raw scores — the engine and
/// the job can never diverge.
/// </summary>
[Collection("worker-sql")]
public sealed class RatingRecomputeTests
{
    private readonly WorkerSqlServerFixture _fixture;

    public RatingRecomputeTests(WorkerSqlServerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Materialized_aggregate_matches_the_shared_kernel_bayesian_formula()
    {
        await ResetRatingsAsync();

        var restaurantId = Guid.NewGuid();
        var rawScores = new[] { 5, 4, 5, 3, 4, 5 }; // sum 26, count 6
        await SeedRawRatingsAsync(restaurantId, rawScores);

        // A second venue with a single high score — proves smoothing pulls a low-count venue toward m.
        var sparseRestaurantId = Guid.NewGuid();
        var singleScore = new[] { 5 };
        await SeedRawRatingsAsync(sparseRestaurantId, singleScore);

        var factory = new SqlConnectionFactory(_fixture.ConnectionString);
        var repository = new DapperRatingAggregateRepository(factory);
        var recomputer = new DapperRatingAggregateRecomputer(factory, repository);

        // --- Act: the rating-recompute job's work --------------------------------------------------
        var written = await recomputer.RecomputeAllAsync();
        written.Should().Be(2);

        // --- Assert: materialized sum/count, and the smoothed value matches the domain formula ------
        var aggregate = await repository.GetByRestaurantAsync(RestaurantRef.From(restaurantId));
        aggregate.Should().NotBeNull();
        aggregate!.Sum.Should().Be(rawScores.Sum());
        aggregate.Count.Should().Be(rawScores.Length);

        var expectedSmoothed = BayesianRatingSmoothing.Smooth(
            rawScores.Sum(),
            rawScores.Length,
            BayesianRatingSmoothing.DefaultPriorWeight,
            BayesianRatingSmoothing.DefaultGlobalMean);

        var materializedSmoothed = BayesianRatingSmoothing.Smooth(
            aggregate.Sum,
            aggregate.Count,
            BayesianRatingSmoothing.DefaultPriorWeight,
            BayesianRatingSmoothing.DefaultGlobalMean);

        materializedSmoothed.Should().Be(expectedSmoothed);

        // The single high-score venue is smoothed toward the global mean, not up to 5.
        var sparse = await repository.GetByRestaurantAsync(RestaurantRef.From(sparseRestaurantId));
        sparse.Should().NotBeNull();
        var sparseSmoothed = BayesianRatingSmoothing.Smooth(
            sparse!.Sum, sparse.Count,
            BayesianRatingSmoothing.DefaultPriorWeight, BayesianRatingSmoothing.DefaultGlobalMean);
        sparseSmoothed.Should().BeLessThan(5d).And.BeGreaterThan(BayesianRatingSmoothing.DefaultGlobalMean);
    }

    private async Task ResetRatingsAsync()
    {
        await using var conn = new SqlConnection(_fixture.ConnectionString);
        await conn.ExecuteAsync(
            """
            DELETE FROM ratings.RatingAggregate;
            DELETE FROM ratings.Rating;
            """);
    }

    private async Task SeedRawRatingsAsync(Guid restaurantId, IReadOnlyList<int> scores)
    {
        await using var conn = new SqlConnection(_fixture.ConnectionString);
        foreach (var score in scores)
        {
            await conn.ExecuteAsync(
                """
                INSERT INTO ratings.Rating (Id, RestaurantId, UserId, Score, GivenAt)
                VALUES (@Id, @RestaurantId, @UserId, @Score, SYSDATETIMEOFFSET());
                """,
                new
                {
                    Id = Guid.NewGuid(),
                    RestaurantId = restaurantId,
                    UserId = Guid.NewGuid(),
                    Score = score,
                });
        }
    }
}
