using FluentAssertions;
using WhereToEat.Recommendation.Domain;
using WhereToEat.Recommendation.Domain.Matching;
using WhereToEat.Recommendation.Domain.Scoring;
using WhereToEat.Recommendation.Domain.Sorting;
using WhereToEat.SharedKernel.ValueObjects;
using Xunit;

namespace WhereToEat.Recommendation.Domain.UnitTests;

/// <summary>
/// Invariant #4 (pluggable engine): a brand-new match/sort strategy can be added and run by the
/// pipeline <b>without editing the pipeline or any existing strategy</b>. These tests register a
/// trivial <c>K-of-N</c> match-stub and a custom sort-stub — both defined here in the test assembly,
/// touching no production code — and feed them straight to the unchanged
/// <see cref="RecommendationPipeline"/>, proving new modes self-register behind the same interfaces.
/// </summary>
public sealed class PluggabilityTests
{
    /// <summary>
    /// A future "at least K of the N selected items" predicate — exactly the seam CLAUDE.md §6 calls
    /// out. It is defined ONLY in the test assembly and implements the same <see cref="IMatchStrategy"/>
    /// the pipeline already consumes; no production type changed to accommodate it.
    /// </summary>
    private sealed class KOfNMatchStrategy(int k) : IMatchStrategy
    {
        public string Key => "k-of-n";

        public Money? Qualify(RestaurantCandidate candidate, int selectionCount)
        {
            if (candidate.MatchedItems.Count < k)
            {
                return null;
            }

            // Basket = the cheapest matched item (same convention as OR for this stub).
            return candidate.MatchedItems.Min(i => i.Price.Amount) is var min
                ? candidate.MatchedItems.First(i => i.Price.Amount == min).Price
                : null;
        }
    }

    /// <summary>A custom sort-stub: rank by rating count descending. New key, no pipeline change.</summary>
    private sealed class MostReviewedSortStrategy : ISortStrategy
    {
        public string Key => "most-reviewed";

        public IReadOnlyList<AggregatedCandidate> Rank(
            IReadOnlyList<AggregatedCandidate> candidates,
            ScoringContext context) =>
            candidates.OrderByDescending(c => c.RatingCount).ThenBy(c => c.RestaurantId).ToList();
    }

    [Fact]
    public void NewMatchStrategy_RunsThroughTheUnchangedPipeline()
    {
        var twoOfThree = TestData.Candidate(matchedPrices: [100m, 80m]);   // 2 matched — qualifies for K=2
        var oneOfThree = TestData.Candidate(matchedPrices: [50m]);          // 1 matched — dropped by K=2

        var pipeline = new RecommendationPipeline();
        var context = new ScoringContext(new RecommendationModeOptions(), null, maxCoverage: 3, distanceEnabled: false);

        var ranked = pipeline.Run(
            [twoOfThree, oneOfThree],
            selectionCount: 3,
            new KOfNMatchStrategy(k: 2),   // the brand-new strategy, plugged straight in
            ExplicitSortStrategy.Price(),
            filters: [],
            context);

        ranked.Should().ContainSingle("only the 2-of-3 venue clears K=2");
        ranked[0].BasketPrice.Amount.Should().Be(80m);
    }

    [Fact]
    public void NewSortStrategy_RunsThroughTheUnchangedPipeline()
    {
        var fewReviews = TestData.Candidate(smoothedRating: 4.9d, ratingCount: 3, matchedPrices: [100m]);
        var manyReviews = TestData.Candidate(smoothedRating: 4.1d, ratingCount: 500, matchedPrices: [100m]);

        var pipeline = new RecommendationPipeline();
        var context = new ScoringContext(new RecommendationModeOptions(), null, maxCoverage: 1, distanceEnabled: false);

        var ranked = pipeline.Run(
            [fewReviews, manyReviews],
            selectionCount: 1,
            new OrMatchStrategy(),
            new MostReviewedSortStrategy(),   // the brand-new sort, plugged straight in
            filters: [],
            context);

        ranked[0].RatingCount.Should().Be(500, "the custom sort ranks by review count");
    }
}
