using FluentAssertions;
using WhereToEat.BuildingBlocks.Persistence;
using WhereToEat.Ratings.Domain;
using WhereToEat.Ratings.Domain.Identifiers;
using WhereToEat.Ratings.Infrastructure.Persistence;
using Xunit;

namespace WhereToEat.Ratings.Integration;

/// <summary>
/// Step 5 containerized-SQL integration tests for the Ratings module: the migrations create the
/// ratings schema, and the Dapper rating-aggregate repository round-trips the cumulative rollup (the
/// hot read path) — including the idempotent upsert the Step 12 recompute job uses. Runs against a real
/// SQL Server container (Docker required).
/// </summary>
public sealed class RatingAggregatePersistenceTests : IClassFixture<SqlServerFixture>
{
    private readonly SqlServerFixture _fixture;
    private readonly DapperRatingAggregateRepository _repository;

    public RatingAggregatePersistenceTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
        _repository = new DapperRatingAggregateRepository(new SqlConnectionFactory(fixture.ConnectionString));
    }

    [Fact]
    public void Migrations_CreatedTheRatingsSchema()
    {
        // The fixture applies 0000-0009; assert the ratings script ran.
        _fixture.AppliedScripts.Should().Contain(s => s.Contains("0004_create_ratings"));
    }

    [Fact]
    public async Task Aggregate_RoundTrips_TheCumulativeRollup()
    {
        var venue = RestaurantRef.From(Guid.NewGuid());
        var aggregate = RatingAggregate.Create(venue, sum: 45d, count: 10L).Value;

        await _repository.UpsertAsync(aggregate);

        var loaded = await _repository.GetByRestaurantAsync(venue);

        loaded.Should().NotBeNull();
        loaded!.Restaurant.Should().Be(venue);
        loaded.Sum.Should().Be(45d);
        loaded.Count.Should().Be(10L);

        // The smoothed value the recommendation engine consumes matches the authoritative formula.
        loaded.SmoothedValue(RatingSmoothingOptions.Default)
            .Should().Be(BayesianSmoothing.Smooth(45d, 10L, RatingSmoothingOptions.Default));

        loaded.DomainEvents.Should().BeEmpty("a read must not surface stale domain events");
    }

    [Fact]
    public async Task Upsert_IsIdempotent_OverwritingTheRollup()
    {
        var venue = RestaurantRef.From(Guid.NewGuid());

        await _repository.UpsertAsync(RatingAggregate.Create(venue, sum: 10d, count: 3L).Value);
        // The Step 12 recompute job rewrites the same key with fresh totals.
        await _repository.UpsertAsync(RatingAggregate.Create(venue, sum: 40d, count: 9L).Value);

        var loaded = await _repository.GetByRestaurantAsync(venue);

        loaded.Should().NotBeNull();
        loaded!.Sum.Should().Be(40d);
        loaded.Count.Should().Be(9L);
    }

    [Fact]
    public async Task GetByRestaurant_ReturnsNull_WhenNoAggregateExists()
    {
        var loaded = await _repository.GetByRestaurantAsync(RestaurantRef.From(Guid.NewGuid()));

        loaded.Should().BeNull("a venue with no reviews has no aggregate row; callers treat it as neutral");
    }
}
