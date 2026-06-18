using FluentAssertions;
using WhereToEat.Recommendation.Domain;
using WhereToEat.Recommendation.Domain.Filtering;
using WhereToEat.Recommendation.Domain.Matching;
using WhereToEat.Recommendation.Domain.Scoring;
using WhereToEat.Recommendation.Domain.Sorting;
using Xunit;

namespace WhereToEat.Recommendation.Domain.UnitTests;

/// <summary>
/// End-to-end over the pure-domain pipeline (Steps 1–5 wired together): match drops/qualifies and
/// prices the basket, aggregation carries coverage, the sort ranks (explicit dominates), and the
/// filters compose over the ranked list. Proves the same coverage/price semantics hold when the
/// strategies run together, not just in isolation.
/// </summary>
public sealed class RecommendationPipelineTests
{
    private static readonly RecommendationPipeline Pipeline = new();

    private static ScoringContext Context(int maxCoverage, bool distanceEnabled = false, decimal? median = null) =>
        new(new RecommendationModeOptions(), median, maxCoverage, distanceEnabled);

    [Fact]
    public void Run_Or_PriceSort_CheapestFirst_DropsNonMatchingByMatchPredicate()
    {
        // Two qualifying venues + one with no matched items (dropped by OR).
        var cheap = TestData.Candidate(matchedPrices: [70m]);
        var pricey = TestData.Candidate(matchedPrices: [130m, 90m]);
        var empty = TestData.Candidate(matchedPrices: Array.Empty<decimal>());

        var ranked = Pipeline.Run(
            [pricey, empty, cheap],
            selectionCount: 2,
            new OrMatchStrategy(),
            ExplicitSortStrategy.Price(),
            filters: [],
            Context(maxCoverage: 2));

        ranked.Should().HaveCount(2, "the no-match venue is dropped");
        ranked[0].BasketPrice.Amount.Should().Be(70m, "cheapest first (price dominates)");
        // The pricey venue's OR basket is its cheapest matched item (90), with coverage 2.
        ranked[1].BasketPrice.Amount.Should().Be(90m);
        ranked[1].Coverage.Should().Be(2);
    }

    [Fact]
    public void Run_And_OnlyFullComboVenuesSurvive_WithSummedBasket()
    {
        var fullCombo = TestData.Candidate(matchedPrices: [80m, 60m]);
        var partial = TestData.Candidate(matchedPrices: [80m]);

        var ranked = Pipeline.Run(
            [fullCombo, partial],
            selectionCount: 2,
            new AndMatchStrategy(),
            ExplicitSortStrategy.Price(),
            filters: [],
            Context(maxCoverage: 2));

        ranked.Should().ContainSingle("only the full combo survives AND");
        ranked[0].BasketPrice.Amount.Should().Be(140m, "AND basket is the combo sum");
        ranked[0].Coverage.Should().Be(2);
    }

    [Fact]
    public void Run_AppliesFiltersAfterRanking_Composably()
    {
        var cheap = TestData.Candidate(smoothedRating: 2.0d, ratingCount: 50, matchedPrices: [60m]);
        var midPriceHighRated = TestData.Candidate(smoothedRating: 4.6d, ratingCount: 50, matchedPrices: [110m]);
        var expensive = TestData.Candidate(smoothedRating: 4.9d, ratingCount: 50, matchedPrices: [300m]);

        var ranked = Pipeline.Run(
            [cheap, midPriceHighRated, expensive],
            selectionCount: 1,
            new OrMatchStrategy(),
            ExplicitSortStrategy.Price(),
            // Compose two filters: price ≤ 150 AND rating ≥ 4.0. Only the mid venue clears both.
            filters:
            [
                (new MaxPriceFilter(), "150"),
                (new MinRatingFilter(), "4.0"),
            ],
            Context(maxCoverage: 1));

        ranked.Should().ContainSingle();
        ranked[0].BasketPrice.Amount.Should().Be(110m);
    }

    [Fact]
    public void Run_DistanceDisabled_LeavesDistanceNull_OnAggregatedCandidates()
    {
        var candidate = TestData.Candidate(distanceKm: 3d, matchedPrices: [100m]);

        var ranked = Pipeline.Run(
            [candidate],
            selectionCount: 1,
            new OrMatchStrategy(),
            ExplicitSortStrategy.Price(),
            filters: [],
            Context(maxCoverage: 1, distanceEnabled: false));

        ranked[0].DistanceKm.Should().BeNull("distance is dropped when ranking has it disabled");
    }
}
