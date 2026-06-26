using FluentAssertions;
using WhereToEat.BuildingBlocks.Persistence;
using WhereToEat.Ratings.Domain;
using WhereToEat.Ratings.Domain.Identifiers;
using WhereToEat.Ratings.Infrastructure.Persistence;
using Xunit;

namespace WhereToEat.Ratings.Integration;

/// <summary>
/// Step 21 containerized-SQL integration tests for the Ratings <b>write side</b>: the Dapper
/// <see cref="DapperRatingRepository"/> round-trips the raw per-user <c>ratings.Rating</c> fact —
/// insert a new rating, look it up by (restaurant, user), and revise it in place. The load-bearing
/// assertion is that a revise UPDATES the same row (one rating per user per restaurant, backed by
/// <c>UQ_Rating_Restaurant_User</c>) and never inserts a duplicate. Runs against a real SQL Server
/// container (Docker required), sharing the migrated-schema fixture with the aggregate suite.
/// </summary>
public sealed class RatingPersistenceTests : IClassFixture<SqlServerFixture>
{
    private readonly DapperRatingRepository _repository;

    public RatingPersistenceTests(SqlServerFixture fixture)
    {
        _repository = new DapperRatingRepository(new SqlConnectionFactory(fixture.ConnectionString));
    }

    [Fact]
    public async Task GetByRestaurantAndUser_ReturnsNull_WhenTheUserHasNotRated()
    {
        var loaded = await _repository.GetByRestaurantAndUserAsync(
            RestaurantRef.From(Guid.NewGuid()), UserRef.From(Guid.NewGuid()));

        loaded.Should().BeNull("a user who has not rated this venue has no Rating row");
    }

    [Fact]
    public async Task Add_ThenGet_RoundTripsTheRawRating()
    {
        var restaurant = RestaurantRef.From(Guid.NewGuid());
        var user = UserRef.From(Guid.NewGuid());
        var givenAt = new DateTimeOffset(2026, 6, 25, 9, 30, 0, TimeSpan.Zero);
        var rating = Rating.Create(restaurant, user, score: 4, givenAt: givenAt).Value;

        await _repository.AddAsync(rating);

        var loaded = await _repository.GetByRestaurantAndUserAsync(restaurant, user);

        loaded.Should().NotBeNull();
        loaded!.Id.Should().Be(rating.Id);
        loaded.Restaurant.Should().Be(restaurant);
        loaded.User.Should().Be(user);
        loaded.Score.Should().Be(4);
        loaded.GivenAt.Should().Be(givenAt);
    }

    [Fact]
    public async Task Revise_UpdatesTheSameRow_WithoutDuplicating()
    {
        var restaurant = RestaurantRef.From(Guid.NewGuid());
        var user = UserRef.From(Guid.NewGuid());
        var created = Rating.Create(
            restaurant, user, score: 2, givenAt: new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero)).Value;
        await _repository.AddAsync(created);

        // The user revises: load the same fact, change the score, update in place.
        var existing = await _repository.GetByRestaurantAndUserAsync(restaurant, user);
        existing.Should().NotBeNull();
        var revisedAt = new DateTimeOffset(2026, 6, 25, 0, 0, 0, TimeSpan.Zero);
        existing!.Revise(score: 5, revisedAt: revisedAt).IsSuccess.Should().BeTrue();

        await _repository.UpdateAsync(existing);

        var reloaded = await _repository.GetByRestaurantAndUserAsync(restaurant, user);
        reloaded.Should().NotBeNull();
        reloaded!.Id.Should().Be(created.Id, "a revise updates the SAME row, it never inserts a second");
        reloaded.Score.Should().Be(5);
        reloaded.GivenAt.Should().Be(revisedAt);
    }
}
