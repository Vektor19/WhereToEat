using FluentAssertions;
using WhereToEat.Recommendation.Domain;
using WhereToEat.Recommendation.Domain.Scoring;
using Xunit;

namespace WhereToEat.Recommendation.Domain.UnitTests;

/// <summary>
/// The four normalized scoring functions. The headline assertion (the cross-module boundary): the
/// quality function reads the <b>smoothed-rating field carried on the candidate</b> and never
/// recomputes it — there is no <c>Ratings.Domain</c> type anywhere in these tests or in the function.
/// The other functions pin their normalization shape (median-relative price, distance decay, coverage).
/// </summary>
public sealed class ScoringFunctionTests
{
    private static ScoringContext Context(
        decimal? median = 100m,
        int maxCoverage = 3,
        bool distanceEnabled = true,
        RecommendationModeOptions? options = null) =>
        new(options ?? new RecommendationModeOptions(), median, maxCoverage, distanceEnabled);

    // ---- f_quality (consumes the candidate DTO's smoothed-rating field; NO Ratings.Domain) -------

    [Theory]
    [InlineData(5.0d, 1.0d)]   // 5★ on a 5 scale => 1.0
    [InlineData(2.5d, 0.5d)]   // 2.5★ => 0.5
    [InlineData(0.0d, 0.0d)]   // 0★ => 0.0
    public void FQuality_NormalizesTheSmoothedRatingCarriedOnTheCandidate(double smoothed, double expected)
    {
        // The smoothed value is a PLAIN FIELD on the candidate — pre-computed by the Ratings module
        // and consumed here. This test never touches a Ratings.Domain type; f_quality only reads it.
        var candidate = TestData.Aggregated(basketAmount: 100m, smoothedRating: smoothed, ratingCount: 100);

        new FQuality().Score(candidate, Context()).Should().BeApproximately(expected, 1e-9);
    }

    [Fact]
    public void FQuality_UnratedVenueScoresTheConfiguredNeutral_NotZero()
    {
        var options = new RecommendationModeOptions { NeutralNormalizedQuality = 0.5d };
        var unrated = TestData.Aggregated(basketAmount: 100m, smoothedRating: null);

        // No rating => neutral, so an unrated venue is not treated as the worst venue.
        new FQuality().Score(unrated, Context(options: options)).Should().Be(0.5d);
    }

    // ---- f_price ---------------------------------------------------------------------------------

    [Theory]
    [InlineData(50d, 0.75d)]   // half the median => cheaper => higher score
    [InlineData(100d, 0.5d)]   // at the median => 0.5
    [InlineData(200d, 0.0d)]   // twice the median => 0
    public void FPrice_IsCheaperRelativeToTheMedian(double basket, double expected)
    {
        var candidate = TestData.Aggregated(basketAmount: (decimal)basket);

        new FPrice().Score(candidate, Context(median: 100m)).Should().BeApproximately(expected, 1e-9);
    }

    [Fact]
    public void FPrice_NoMedian_FallsBackToNeutral()
    {
        var candidate = TestData.Aggregated(basketAmount: 100m);

        new FPrice().Score(candidate, Context(median: null)).Should().Be(0.5d);
    }

    // ---- f_distance ------------------------------------------------------------------------------

    [Theory]
    [InlineData(0d, 1.0d)]     // at the user => 1
    [InlineData(5d, 0.5d)]     // halfway to the 10km radius => 0.5
    [InlineData(10d, 0.0d)]    // at the radius => 0
    [InlineData(20d, 0.0d)]    // beyond the radius => clamped to 0 (not excluded)
    public void FDistance_DecaysLinearlyToTheRadius(double distance, double expected)
    {
        var options = new RecommendationModeOptions { DistanceNormalizationRadiusKm = 10d };
        var candidate = TestData.Aggregated(basketAmount: 100m, distanceKm: distance);

        new FDistance().Score(candidate, Context(distanceEnabled: true, options: options))
            .Should().BeApproximately(expected, 1e-9);
    }

    [Fact]
    public void FDistance_Disabled_IsNeutral()
    {
        var candidate = TestData.Aggregated(basketAmount: 100m, distanceKm: 9d);

        // User disabled/omitted geo => distance is not a factor, scores neutral 1.
        new FDistance().Score(candidate, Context(distanceEnabled: false)).Should().Be(1d);
    }

    // ---- f_coverage ------------------------------------------------------------------------------

    [Theory]
    [InlineData(3, 1.0d)]
    [InlineData(2, 2d / 3d)]
    [InlineData(1, 1d / 3d)]
    public void FCoverage_NormalizesByTheSelectionCount(int coverage, double expected)
    {
        var candidate = TestData.Aggregated(basketAmount: 100m, coverage: coverage);

        new FCoverage().Score(candidate, Context(maxCoverage: 3)).Should().BeApproximately(expected, 1e-9);
    }
}
