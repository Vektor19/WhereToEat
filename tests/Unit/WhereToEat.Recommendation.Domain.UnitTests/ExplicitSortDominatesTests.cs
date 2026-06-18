using FluentAssertions;
using WhereToEat.Recommendation.Domain;
using WhereToEat.Recommendation.Domain.Scoring;
using WhereToEat.Recommendation.Domain.Sorting;
using Xunit;

namespace WhereToEat.Recommendation.Domain.UnitTests;

/// <summary>
/// The CRITICAL invariant #5: under an <b>explicit</b> sort (price/distance/rating) the chosen field
/// is the <b>primary</b> key and coverage is <b>only a tie-breaker</b>. These tests prove both halves
/// for all three explicit modes: (1) a worse-coverage but better-on-primary venue ranks above a
/// full-coverage but worse-on-primary venue (the field dominates — the cheapest 1-of-3 beats the
/// pricier 3-of-3); (2) when two venues tie on the primary field, the higher-coverage one ranks first.
/// </summary>
public sealed class ExplicitSortDominatesTests
{
    private static ScoringContext Context() =>
        new(new RecommendationModeOptions(), medianBasketAmount: null, maxCoverage: 3, distanceEnabled: true);

    // ---- PRICE -----------------------------------------------------------------------------------

    [Fact]
    public void Price_CheapestWins_EvenWhenItHasLowerCoverage()
    {
        var cheap1Of3 = TestData.Aggregated(basketAmount: 80m, coverage: 1);
        var pricey3Of3 = TestData.Aggregated(basketAmount: 200m, coverage: 3);

        var ranked = ExplicitSortStrategy.Price().Rank([pricey3Of3, cheap1Of3], Context());

        // Cheapest first — price dominates, the 3-of-3 does NOT jump it (invariant #5).
        ranked[0].Should().BeSameAs(cheap1Of3);
        ranked[1].Should().BeSameAs(pricey3Of3);
    }

    [Fact]
    public void Price_EqualPrice_HigherCoverageBreaksTheTie()
    {
        var equalPriceLowCoverage = TestData.Aggregated(basketAmount: 120m, coverage: 1);
        var equalPriceHighCoverage = TestData.Aggregated(basketAmount: 120m, coverage: 3);

        var ranked = ExplicitSortStrategy.Price().Rank([equalPriceLowCoverage, equalPriceHighCoverage], Context());

        // Same price => coverage (the tie-breaker) lifts the 3-of-3 above the 1-of-3.
        ranked[0].Should().BeSameAs(equalPriceHighCoverage);
        ranked[1].Should().BeSameAs(equalPriceLowCoverage);
    }

    // ---- DISTANCE --------------------------------------------------------------------------------

    [Fact]
    public void Distance_NearestWins_EvenWhenItHasLowerCoverage()
    {
        var near1Of3 = TestData.Aggregated(basketAmount: 100m, distanceKm: 0.5d, coverage: 1);
        var far3Of3 = TestData.Aggregated(basketAmount: 100m, distanceKm: 8d, coverage: 3);

        var ranked = ExplicitSortStrategy.Distance().Rank([far3Of3, near1Of3], Context());

        ranked[0].Should().BeSameAs(near1Of3);
        ranked[1].Should().BeSameAs(far3Of3);
    }

    [Fact]
    public void Distance_EqualDistance_HigherCoverageBreaksTheTie()
    {
        var lowCoverage = TestData.Aggregated(basketAmount: 100m, distanceKm: 2d, coverage: 1);
        var highCoverage = TestData.Aggregated(basketAmount: 100m, distanceKm: 2d, coverage: 3);

        var ranked = ExplicitSortStrategy.Distance().Rank([lowCoverage, highCoverage], Context());

        ranked[0].Should().BeSameAs(highCoverage);
        ranked[1].Should().BeSameAs(lowCoverage);
    }

    // ---- RATING ----------------------------------------------------------------------------------

    [Fact]
    public void Rating_HighestWins_EvenWhenItHasLowerCoverage()
    {
        var highRated1Of3 = TestData.Aggregated(basketAmount: 100m, smoothedRating: 4.8d, ratingCount: 200, coverage: 1);
        var lowRated3Of3 = TestData.Aggregated(basketAmount: 100m, smoothedRating: 3.2d, ratingCount: 200, coverage: 3);

        var ranked = ExplicitSortStrategy.Rating().Rank([lowRated3Of3, highRated1Of3], Context());

        ranked[0].Should().BeSameAs(highRated1Of3);
        ranked[1].Should().BeSameAs(lowRated3Of3);
    }

    [Fact]
    public void Rating_EqualRating_HigherCoverageBreaksTheTie()
    {
        var lowCoverage = TestData.Aggregated(basketAmount: 100m, smoothedRating: 4.5d, ratingCount: 50, coverage: 1);
        var highCoverage = TestData.Aggregated(basketAmount: 100m, smoothedRating: 4.5d, ratingCount: 50, coverage: 3);

        var ranked = ExplicitSortStrategy.Rating().Rank([lowCoverage, highCoverage], Context());

        ranked[0].Should().BeSameAs(highCoverage);
        ranked[1].Should().BeSameAs(lowCoverage);
    }

    [Fact]
    public void Rating_UnratedVenueSortsLast_OnThePrimaryKey()
    {
        var rated = TestData.Aggregated(basketAmount: 100m, smoothedRating: 3.0d, ratingCount: 10, coverage: 3);
        var unrated = TestData.Aggregated(basketAmount: 100m, smoothedRating: null, coverage: 3);

        var ranked = ExplicitSortStrategy.Rating().Rank([unrated, rated], Context());

        // An unrated venue is not excluded, but it sorts last on the primary (rating) key.
        ranked[0].Should().BeSameAs(rated);
        ranked[1].Should().BeSameAs(unrated);
    }
}
