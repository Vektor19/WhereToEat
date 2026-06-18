using FluentAssertions;
using WhereToEat.Recommendation.Domain;
using WhereToEat.Recommendation.Domain.Matching;
using Xunit;

namespace WhereToEat.Recommendation.Domain.UnitTests;

/// <summary>
/// Step 1 predicates (CLAUDE.md §6): <c>OR</c> keeps venues with ≥1 selected item and prices the
/// basket at the cheapest matched item; <c>AND</c> keeps only venues with all selected items and
/// prices the basket at the combo sum. Coverage (the matched-item count) is verified to be computed
/// correctly for both — full under AND by definition, partial-allowed under OR.
/// </summary>
public sealed class MatchStrategyTests
{
    // ---- OR --------------------------------------------------------------------------------------

    [Fact]
    public void Or_QualifiesAVenueWithAtLeastOneSelectedItem()
    {
        var candidate = TestData.Candidate(matchedPrices: [120m]);

        // Selection of 3 items, but the venue has only 1 — OR still qualifies it.
        var basket = new OrMatchStrategy().Qualify(candidate, selectionCount: 3);

        basket.Should().NotBeNull();
        candidate.MatchedItems.Count.Should().Be(1, "coverage is the matched-item count");
    }

    [Fact]
    public void Or_BasketPriceIsTheCheapestMatchedItem()
    {
        var candidate = TestData.Candidate(matchedPrices: [150m, 90m, 200m]);

        var basket = new OrMatchStrategy().Qualify(candidate, selectionCount: 3);

        basket!.Amount.Should().Be(90m, "OR buys the single cheapest of the matched items");
        candidate.MatchedItems.Count.Should().Be(3, "coverage = 3-of-3 here");
    }

    [Fact]
    public void Or_DropsAVenueWithNoMatchedItems()
    {
        var candidate = TestData.Candidate(matchedPrices: Array.Empty<decimal>());

        new OrMatchStrategy().Qualify(candidate, selectionCount: 2).Should().BeNull();
    }

    // ---- AND -------------------------------------------------------------------------------------

    [Fact]
    public void And_QualifiesOnlyWhenAllSelectedItemsArePresent()
    {
        var fullCombo = TestData.Candidate(matchedPrices: [80m, 60m]);
        var partial = TestData.Candidate(matchedPrices: [80m]);

        var strategy = new AndMatchStrategy();

        strategy.Qualify(fullCombo, selectionCount: 2).Should().NotBeNull("all 2 present");
        strategy.Qualify(partial, selectionCount: 2).Should().BeNull("only 1-of-2 present — the combo gate fails");
    }

    [Fact]
    public void And_BasketPriceIsTheSumOfTheComboItems()
    {
        var combo = TestData.Candidate(matchedPrices: [80m, 60m]);

        var basket = new AndMatchStrategy().Qualify(combo, selectionCount: 2);

        basket!.Amount.Should().Be(140m, "AND buys the whole combo — the sum");
        combo.MatchedItems.Count.Should().Be(2, "coverage is full under AND by definition");
    }

    [Theory]
    [InlineData("or", typeof(OrMatchStrategy))]
    [InlineData("and", typeof(AndMatchStrategy))]
    public void Match_KeysAreStable(string expectedKey, Type strategyType)
    {
        var strategy = (IMatchStrategy)Activator.CreateInstance(strategyType)!;
        strategy.Key.Should().Be(expectedKey);
    }
}
