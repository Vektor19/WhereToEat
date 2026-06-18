using FluentAssertions;
using WhereToEat.Recommendation.Domain;
using WhereToEat.Recommendation.Domain.Scoring;
using WhereToEat.Recommendation.Domain.Sorting;
using Xunit;

namespace WhereToEat.Recommendation.Domain.UnitTests;

/// <summary>
/// Composite-mode ranking (CLAUDE.md §6 Step 3 (B)): a deterministic ordering from the weighted,
/// normalized <c>f_*</c> sum, and — the key property — <b>changing a mode weight changes the
/// ordering</b>. The tests build two venues that trade off price vs. quality, then show a
/// price-heavy weighting and a quality-heavy weighting rank them in opposite orders.
/// </summary>
public sealed class CompositeScoreTests
{
    private static readonly IReadOnlyList<IScoringFunction> Functions =
    [
        new FPrice(),
        new FQuality(),
        new FDistance(),
        new FCoverage(),
    ];

    private static ScoringContext Context() =>
        // Median 100 so f_price has a market reference; distance enabled with equal distances so it
        // is neutral between the two venues; max coverage 1.
        new(new RecommendationModeOptions(), medianBasketAmount: 100m, maxCoverage: 1, distanceEnabled: true);

    [Fact]
    public void Composite_ProducesADeterministicOrdering()
    {
        var cheap = TestData.Aggregated(basketAmount: 60m, smoothedRating: 3.0d, ratingCount: 100, distanceKm: 1d);
        var premium = TestData.Aggregated(basketAmount: 140m, smoothedRating: 4.8d, ratingCount: 100, distanceKm: 1d);

        var weights = new Dictionary<string, double>
        {
            [FPrice.FactorKey] = 0.5d,
            [FQuality.FactorKey] = 0.5d,
        };
        var strategy = new CompositeScoreStrategy("balanced", Functions, weights);

        var first = strategy.Rank([cheap, premium], Context());
        var again = strategy.Rank([premium, cheap], Context());

        // Order is a pure function of the figures — independent of input order.
        first.Select(c => c.RestaurantId).Should().Equal(again.Select(c => c.RestaurantId));
    }

    [Fact]
    public void Composite_WeightSensitivity_PriceHeavyVsQualityHeavyFlipTheOrder()
    {
        // Cheap-but-mediocre vs. premium-but-excellent, equal distance/coverage so only price &
        // quality differentiate them.
        var cheap = TestData.Aggregated(basketAmount: 50m, smoothedRating: 3.0d, ratingCount: 100, distanceKm: 1d);
        var premium = TestData.Aggregated(basketAmount: 150m, smoothedRating: 5.0d, ratingCount: 100, distanceKm: 1d);

        var priceHeavy = new CompositeScoreStrategy(
            "price-heavy",
            Functions,
            new Dictionary<string, double> { [FPrice.FactorKey] = 0.9d, [FQuality.FactorKey] = 0.1d });

        var qualityHeavy = new CompositeScoreStrategy(
            "quality-heavy",
            Functions,
            new Dictionary<string, double> { [FPrice.FactorKey] = 0.1d, [FQuality.FactorKey] = 0.9d });

        var byPrice = priceHeavy.Rank([cheap, premium], Context());
        var byQuality = qualityHeavy.Rank([cheap, premium], Context());

        // Price-heavy => the cheap venue wins; quality-heavy => the premium venue wins. Changing the
        // weight changed the ordering — the weight-sensitivity invariant.
        byPrice[0].Should().BeSameAs(cheap);
        byQuality[0].Should().BeSameAs(premium);
    }

    [Fact]
    public void Composite_ScoreOf_IsTheWeightedSumOfNormalizedFunctions()
    {
        // basket 100 vs median 100 => f_price = 1 - (1/2) = 0.5; rating 5/5 => f_quality = 1;
        // distance disabled => f_distance neutral 1; coverage 1/1 => f_coverage 1.
        var candidate = TestData.Aggregated(basketAmount: 100m, smoothedRating: 5.0d, ratingCount: 10, coverage: 1);
        var context = new ScoringContext(new RecommendationModeOptions(), medianBasketAmount: 100m, maxCoverage: 1, distanceEnabled: false);

        var weights = new Dictionary<string, double>
        {
            [FPrice.FactorKey] = 1d,
            [FQuality.FactorKey] = 2d,
        };
        var strategy = new CompositeScoreStrategy("weighted", Functions, weights);

        // 1*0.5 + 2*1.0 = 2.5 (distance/coverage have weight 0 here).
        strategy.ScoreOf(candidate, context).Should().BeApproximately(2.5d, 1e-9);
    }
}
