namespace WhereToEat.Recommendation.Application.Abstractions;

/// <summary>
/// The candidate-source result DTO — a flat, framework-free shape the read side returns. It carries
/// the venue's <see cref="SmoothedRating"/> as a <b>plain field</b>: the value is computed by the
/// Ratings module and joined in by the infrastructure adapter (a DB join), so the recommendation
/// engine consumes it directly and <b>never</b> references <c>Ratings.Domain</c>. This DTO is the
/// single channel by which the rating crosses the module boundary (invariant boundary the Step 2
/// cross-module fitness test enforces).
/// </summary>
public sealed record RecommendationCandidate(
    Guid RestaurantId,
    string Name,
    IReadOnlyList<MatchedItemDto> MatchedItems,
    double? SmoothedRating,
    int RatingCount,
    double? DistanceKm);

/// <summary>
/// One of the user's selected items found at a venue: which selection it satisfied (exactly one of
/// the ids is set) and the price of the cheapest offering for it there. Coverage is the count of
/// these; the basket price is derived from their prices by the match strategy.
/// </summary>
public sealed record MatchedItemDto(
    Guid? CategoryId,
    Guid? DishId,
    decimal PriceAmount,
    string PriceCurrency);
