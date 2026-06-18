using FluentAssertions;
using WhereToEat.SharedKernel.Ratings;
using Xunit;

namespace WhereToEat.SharedKernel.UnitTests.Ratings;

/// <summary>
/// Pins the <b>single authoritative</b> Bayesian smoothing math and its canonical constants in the
/// SharedKernel (invariant #6). Both the Ratings module and the Recommendation module delegate here,
/// so this is the one place the formula <c>smoothed = (C*m + Sum) / (C + n)</c> and the default
/// <c>C = 10</c>, <c>m = 3.5</c> are verified — guaranteeing the rating the engine ranks on matches
/// the rating the recompute job persists.
/// </summary>
public sealed class BayesianRatingSmoothingTests
{
    [Fact]
    public void CanonicalDefaults_Are_C10_And_M35()
    {
        // The drift the review flagged: the engine and the recompute job must share these constants.
        BayesianRatingSmoothing.DefaultPriorWeight.Should().Be(10d);
        BayesianRatingSmoothing.DefaultGlobalMean.Should().Be(3.5d);
    }

    [Fact]
    public void Count_Zero_ReturnsGlobalMean()
    {
        BayesianRatingSmoothing.Smooth(sum: 0d, count: 0L, priorWeight: 10d, globalMean: 3.5d)
            .Should().Be(3.5d);
    }

    [Fact]
    public void SingleFiveStar_WithDefaultPrior_StaysNearMean()
    {
        // (10*3.5 + 5) / (10 + 1) = 40/11 — a lone 5★ stays near 3.5, far below 5.
        BayesianRatingSmoothing.Smooth(sum: 5d, count: 1L, priorWeight: 10d, globalMean: 3.5d)
            .Should().BeApproximately(40d / 11d, 1e-9);
    }

    [Fact]
    public void LargeCount_ApproachesRawAverage()
    {
        const long count = 100_000L;
        const double raw = 4.5d;

        BayesianRatingSmoothing.Smooth(raw * count, count, 10d, 3.5d)
            .Should().BeApproximately(raw, 1e-3);
    }

    [Theory]
    [InlineData(-1d, 1L)]
    [InlineData(double.NaN, 1L)]
    public void RejectsInvalidSum(double sum, long count)
    {
        var act = () => BayesianRatingSmoothing.Smooth(sum, count, 10d, 3.5d);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void RejectsNegativeCount()
    {
        var act = () => BayesianRatingSmoothing.Smooth(sum: 0d, count: -1L, priorWeight: 10d, globalMean: 3.5d);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
