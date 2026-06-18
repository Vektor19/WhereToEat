using FluentAssertions;
using WhereToEat.Ratings.Domain;
using Xunit;

namespace WhereToEat.Ratings.Domain.UnitTests;

/// <summary>
/// Exhaustive cases for the single authoritative <see cref="BayesianSmoothing"/> formula
/// <c>smoothed = (C*m + Sum) / (C + n)</c> (invariant #6). These pin the properties the design
/// promises: no reviews resolve to the neutral mean m; the prior washes out as the count grows; a
/// single high review stays near m; many consistent reviews approach their raw average; and m/C are
/// configurable.
/// </summary>
public sealed class BayesianSmoothingTests
{
    private const double DefaultPriorWeight = 10d; // C
    private const double DefaultGlobalMean = 3.5d;  // m

    [Fact]
    public void Count_Zero_ReturnsGlobalMean()
    {
        // No reviews => exactly m (never a misleading 0).
        var smoothed = BayesianSmoothing.Smooth(sum: 0d, count: 0L, priorWeight: DefaultPriorWeight, globalMean: DefaultGlobalMean);

        smoothed.Should().Be(DefaultGlobalMean);
    }

    [Fact]
    public void Count_Zero_WithZeroPriorWeight_StillReturnsGlobalMean_NoDivideByZero()
    {
        // The 0/0 edge resolves to m rather than throwing/NaN.
        var smoothed = BayesianSmoothing.Smooth(sum: 0d, count: 0L, priorWeight: 0d, globalMean: 4.2d);

        smoothed.Should().Be(4.2d);
    }

    [Fact]
    public void LargeCount_ApproachesRawAverage_PriorWashesOut()
    {
        // 100_000 reviews all of 4.5 => raw average 4.5; the prior is negligible.
        const long count = 100_000L;
        const double raw = 4.5d;
        var sum = raw * count;

        var smoothed = BayesianSmoothing.Smooth(sum, count, DefaultPriorWeight, DefaultGlobalMean);

        smoothed.Should().BeApproximately(raw, 1e-3);
    }

    [Fact]
    public void HundredsOfConsistent45_Ratings_ApproachRawAverage()
    {
        // The design's concrete case: hundreds of 4.5 ratings should sit very close to 4.5.
        const long count = 500L;
        const double raw = 4.5d;
        var sum = raw * count;

        var smoothed = BayesianSmoothing.Smooth(sum, count, DefaultPriorWeight, DefaultGlobalMean);

        smoothed.Should().BeApproximately(4.5d, 0.05d);
        smoothed.Should().BeLessThan(4.5d, "with a mean of 3.5 below 4.5, the prior pulls very slightly down");
    }

    [Fact]
    public void SingleFiveStar_WithDefaultPrior_StaysNearGlobalMean_NotNearFive()
    {
        // One enthusiastic 5★ must not lift the venue near 5 — it stays close to m.
        var smoothed = BayesianSmoothing.Smooth(sum: 5d, count: 1L, priorWeight: DefaultPriorWeight, globalMean: DefaultGlobalMean);

        // (10*3.5 + 5) / (10 + 1) = 40/11 ≈ 3.636
        smoothed.Should().BeApproximately(40d / 11d, 1e-9);
        smoothed.Should().BeLessThan(4d, "a single 5★ with C=10 stays near the 3.5 mean, far below 5");
    }

    [Fact]
    public void SingleFiveStar_NeverOutranks_StableHighRatedVenue()
    {
        // The intent: a lone 5★ must rank below a venue with hundreds of stable 4.5s.
        var loneFiveStar = BayesianSmoothing.Smooth(sum: 5d, count: 1L, priorWeight: DefaultPriorWeight, globalMean: DefaultGlobalMean);
        var stableHigh = BayesianSmoothing.Smooth(sum: 4.5d * 300, count: 300L, priorWeight: DefaultPriorWeight, globalMean: DefaultGlobalMean);

        stableHigh.Should().BeGreaterThan(loneFiveStar);
    }

    [Theory]
    [InlineData(0d, 4d, 1L, 4d)]      // C=0 => exactly the raw average (no smoothing)
    [InlineData(1d, 4d, 1L, 3.75d)]   // (1*3.5 + 4)/(1+1) = 7.5/2
    [InlineData(100d, 5d, 1L, 3.5148514851485148d)] // big C => clings to m even with a 5★
    public void BoundaryAndConfigurablePriorWeights_BehaveAsExpected(double priorWeight, double sum, long count, double expected)
    {
        var smoothed = BayesianSmoothing.Smooth(sum, count, priorWeight, globalMean: DefaultGlobalMean);

        smoothed.Should().BeApproximately(expected, 1e-9);
    }

    [Theory]
    [InlineData(2d)]
    [InlineData(3.5d)]
    [InlineData(4.8d)]
    public void GlobalMean_IsConfigurable_NoReviewsResolveToIt(double globalMean)
    {
        var smoothed = BayesianSmoothing.Smooth(sum: 0d, count: 0L, priorWeight: DefaultPriorWeight, globalMean: globalMean);

        smoothed.Should().Be(globalMean);
    }

    [Fact]
    public void OptionsOverload_MatchesPrimitiveOverload()
    {
        var options = new RatingSmoothingOptions(priorWeight: 8d, globalMean: 3.2d);

        var viaOptions = BayesianSmoothing.Smooth(sum: 30d, count: 9L, options);
        var viaPrimitives = BayesianSmoothing.Smooth(sum: 30d, count: 9L, priorWeight: 8d, globalMean: 3.2d);

        viaOptions.Should().Be(viaPrimitives);
    }

    [Theory]
    [InlineData(-1d, 1L)]
    [InlineData(double.NaN, 1L)]
    [InlineData(double.PositiveInfinity, 1L)]
    public void Smooth_RejectsInvalidSum(double sum, long count)
    {
        var act = () => BayesianSmoothing.Smooth(sum, count, DefaultPriorWeight, DefaultGlobalMean);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Smooth_RejectsNegativeCount()
    {
        var act = () => BayesianSmoothing.Smooth(sum: 0d, count: -1L, priorWeight: DefaultPriorWeight, globalMean: DefaultGlobalMean);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(-1d)]
    [InlineData(double.NaN)]
    public void Smooth_RejectsInvalidPriorWeight(double priorWeight)
    {
        var act = () => BayesianSmoothing.Smooth(sum: 0d, count: 0L, priorWeight, globalMean: DefaultGlobalMean);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
