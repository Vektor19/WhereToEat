using WhereToEat.Recommendation.Domain;
using WhereToEat.SharedKernel.ValueObjects;

namespace WhereToEat.Recommendation.Domain.UnitTests;

/// <summary>
/// Small builders so the table-driven tests read as data, not ceremony. Money/selection ids are
/// created through the real value objects so the tests exercise the same construction the engine sees.
/// </summary>
internal static class TestData
{
    public const string Currency = "UAH";

    public static Money Uah(decimal amount) => Money.Create(amount, Currency).Value;

    public static SelectionKey Dish() => SelectionKey.Dish(Guid.NewGuid());

    /// <summary>An aggregated candidate with explicit figures — the input the sort strategies rank.</summary>
    public static AggregatedCandidate Aggregated(
        decimal basketAmount,
        double? smoothedRating = null,
        int ratingCount = 0,
        double? distanceKm = null,
        int coverage = 1,
        Guid? id = null) =>
        new(
            id ?? Guid.NewGuid(),
            "Test Venue",
            Uah(basketAmount),
            smoothedRating,
            ratingCount,
            distanceKm,
            coverage);

    /// <summary>A raw candidate with N matched items at the given prices (for OR/AND match tests).</summary>
    public static RestaurantCandidate Candidate(
        double? smoothedRating = null,
        int ratingCount = 0,
        double? distanceKm = null,
        params decimal[] matchedPrices)
    {
        var matched = matchedPrices
            .Select(p => new MatchedItem(Dish(), Uah(p)))
            .ToList();

        return new RestaurantCandidate(Guid.NewGuid(), "Test Venue", matched, smoothedRating, ratingCount, distanceKm);
    }
}
