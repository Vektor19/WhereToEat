using FluentAssertions;
using WhereToEat.Ratings.Domain;
using WhereToEat.Ratings.Domain.Identifiers;
using Xunit;

namespace WhereToEat.Ratings.Domain.UnitTests;

/// <summary>
/// The per-restaurant cumulative rollup: it folds raw scores into Sum/Count and computes the smoothed
/// value via the authoritative formula, so the materialized value always matches BayesianSmoothing.
/// </summary>
public sealed class RatingAggregateTests
{
    private static readonly RestaurantRef Venue = RestaurantRef.From(Guid.NewGuid());

    [Fact]
    public void Empty_HasNoReviews_AndSmoothsToGlobalMean()
    {
        var aggregate = RatingAggregate.Empty(Venue).Value;

        aggregate.Sum.Should().Be(0d);
        aggregate.Count.Should().Be(0L);
        aggregate.SmoothedValue(RatingSmoothingOptions.Default).Should().Be(RatingSmoothingOptions.Default.GlobalMean);
    }

    [Fact]
    public void Create_RejectsDefaultRestaurant()
    {
        var result = RatingAggregate.Create(default, sum: 0d, count: 0L);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("RatingAggregate.RestaurantRequired");
    }

    [Theory]
    [InlineData(-1d, 0L)]
    [InlineData(0d, -1L)]
    public void Create_RejectsInvalidSumOrCount(double sum, long count)
    {
        var result = RatingAggregate.Create(Venue, sum, count);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ApplyNewScore_FoldsSumAndCount()
    {
        var aggregate = RatingAggregate.Empty(Venue).Value;

        aggregate.ApplyNewScore(5).IsSuccess.Should().BeTrue();
        aggregate.ApplyNewScore(4).IsSuccess.Should().BeTrue();

        aggregate.Sum.Should().Be(9d);
        aggregate.Count.Should().Be(2L);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void ApplyNewScore_RejectsOutOfRange(int score)
    {
        var aggregate = RatingAggregate.Empty(Venue).Value;

        aggregate.ApplyNewScore(score).IsFailure.Should().BeTrue();
        aggregate.Count.Should().Be(0L, "a rejected score must not be folded in");
    }

    [Fact]
    public void ApplyScoreRevision_AdjustsSumByDelta_KeepsCount()
    {
        var aggregate = RatingAggregate.Empty(Venue).Value;
        aggregate.ApplyNewScore(3);

        aggregate.ApplyScoreRevision(previousScore: 3, newScore: 5).IsSuccess.Should().BeTrue();

        aggregate.Sum.Should().Be(5d);
        aggregate.Count.Should().Be(1L, "a revision is the same review, not a new one");
    }

    [Fact]
    public void ApplyScoreRevision_OnEmptyAggregate_Fails()
    {
        var aggregate = RatingAggregate.Empty(Venue).Value;

        aggregate.ApplyScoreRevision(previousScore: 3, newScore: 4).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void SmoothedValue_MatchesTheAuthoritativeFormula()
    {
        var aggregate = RatingAggregate.Create(Venue, sum: 45d, count: 10L).Value;
        var options = RatingSmoothingOptions.Default;

        aggregate.SmoothedValue(options)
            .Should().Be(BayesianSmoothing.Smooth(45d, 10L, options));
    }
}
