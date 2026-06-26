using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WhereToEat.Contracts.IntegrationEvents;
using WhereToEat.Ratings.Application.Abstractions;
using WhereToEat.Ratings.Application.Submit;
using WhereToEat.Ratings.Domain;
using WhereToEat.Ratings.Domain.Abstractions;
using WhereToEat.Ratings.Domain.Identifiers;
using WhereToEat.SharedKernel.Results;
using Xunit;

namespace WhereToEat.Ratings.Application.UnitTests;

/// <summary>
/// Unit tests for <see cref="SubmitRatingCommandHandler"/> with a mocked <see cref="IRatingRepository"/>
/// write port and <see cref="IRatingGivenPublisher"/> seam (NSubstitute) and a fixed time source. They
/// prove the load-bearing behaviours of the submit use-case: create when the user has no rating, revise
/// the existing fact when they do (never a duplicate), an out-of-range score returns the domain
/// validation failure WITHOUT persisting or publishing, and <see cref="RatingGiven"/> is published
/// exactly once on success (invariant #6 — the recompute is downstream of this event).
/// </summary>
public sealed class SubmitRatingCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 25, 12, 0, 0, TimeSpan.Zero);

    private readonly IRatingRepository _ratings = Substitute.For<IRatingRepository>();
    private readonly IRatingGivenPublisher _publisher = Substitute.For<IRatingGivenPublisher>();
    private readonly SubmitRatingCommandHandler _handler;

    public SubmitRatingCommandHandlerTests()
    {
        _handler = new SubmitRatingCommandHandler(
            _ratings,
            _publisher,
            new FixedTimeProvider(Now),
            NullLogger<SubmitRatingCommandHandler>.Instance);
    }

    [Fact]
    public async Task Submit_WhenNoRatingExists_CreatesTheFact_AndPublishesRatingGiven()
    {
        var restaurantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _ratings.GetByRestaurantAndUserAsync(
                RestaurantRef.From(restaurantId), UserRef.From(userId), Arg.Any<CancellationToken>())
            .Returns((Rating?)null);

        var result = await _handler.HandleAsync(new SubmitRatingCommand(restaurantId, userId, 4));

        result.IsSuccess.Should().BeTrue();
        // Created a new fact (never an update on the create path), carrying the resolved refs + score.
        await _ratings.Received(1).AddAsync(
            Arg.Is<Rating>(r =>
                r.Restaurant == RestaurantRef.From(restaurantId)
                && r.User == UserRef.From(userId)
                && r.Score == 4
                && r.GivenAt == Now),
            Arg.Any<CancellationToken>());
        await _ratings.DidNotReceive().UpdateAsync(Arg.Any<Rating>(), Arg.Any<CancellationToken>());
        await _publisher.Received(1).PublishAsync(
            Arg.Is<RatingGiven>(e => e.RestaurantId == restaurantId && e.Score == 4 && e.OccurredOnUtc == Now),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_WhenRatingExists_RevisesTheSameFact_AndPublishesRatingGiven()
    {
        var restaurantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var existing = Rating.Create(
            RestaurantRef.From(restaurantId), UserRef.From(userId), score: 2, givenAt: Now.AddDays(-1)).Value;
        _ratings.GetByRestaurantAndUserAsync(
                RestaurantRef.From(restaurantId), UserRef.From(userId), Arg.Any<CancellationToken>())
            .Returns(existing);

        var result = await _handler.HandleAsync(new SubmitRatingCommand(restaurantId, userId, 5));

        result.IsSuccess.Should().BeTrue();
        // Revised IN PLACE: the same aggregate is updated (no new fact inserted), with the new score/time.
        existing.Score.Should().Be(5);
        existing.GivenAt.Should().Be(Now);
        await _ratings.Received(1).UpdateAsync(existing, Arg.Any<CancellationToken>());
        await _ratings.DidNotReceive().AddAsync(Arg.Any<Rating>(), Arg.Any<CancellationToken>());
        await _publisher.Received(1).PublishAsync(
            Arg.Is<RatingGiven>(e => e.RestaurantId == restaurantId && e.Score == 5),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public async Task Submit_WithScoreOutOfRange_ReturnsValidationFailure_AndNeitherPersistsNorPublishes(int score)
    {
        var restaurantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _ratings.GetByRestaurantAndUserAsync(
                RestaurantRef.From(restaurantId), UserRef.From(userId), Arg.Any<CancellationToken>())
            .Returns((Rating?)null);

        var result = await _handler.HandleAsync(new SubmitRatingCommand(restaurantId, userId, score));

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Rating.ScoreOutOfRange");
        await _ratings.DidNotReceive().AddAsync(Arg.Any<Rating>(), Arg.Any<CancellationToken>());
        await _ratings.DidNotReceive().UpdateAsync(Arg.Any<Rating>(), Arg.Any<CancellationToken>());
        await _publisher.DidNotReceive().PublishAsync(Arg.Any<RatingGiven>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_WhenRevisingToAnOutOfRangeScore_ReturnsFailure_AndDoesNotPublish()
    {
        var restaurantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var existing = Rating.Create(
            RestaurantRef.From(restaurantId), UserRef.From(userId), score: 3, givenAt: Now.AddDays(-1)).Value;
        _ratings.GetByRestaurantAndUserAsync(
                RestaurantRef.From(restaurantId), UserRef.From(userId), Arg.Any<CancellationToken>())
            .Returns(existing);

        var result = await _handler.HandleAsync(new SubmitRatingCommand(restaurantId, userId, 99));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Rating.ScoreOutOfRange");
        // The existing fact is untouched and nothing is updated or published.
        existing.Score.Should().Be(3);
        await _ratings.DidNotReceive().UpdateAsync(Arg.Any<Rating>(), Arg.Any<CancellationToken>());
        await _publisher.DidNotReceive().PublishAsync(Arg.Any<RatingGiven>(), Arg.Any<CancellationToken>());
    }

    /// <summary>A minimal fixed <see cref="TimeProvider"/> so the handler stamps a deterministic GivenAt.</summary>
    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedTimeProvider(DateTimeOffset now) => _now = now;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
