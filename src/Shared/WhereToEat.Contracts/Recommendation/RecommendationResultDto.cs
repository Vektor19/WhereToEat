namespace WhereToEat.Contracts.Recommendation;

/// <summary>
/// The public recommendation response: an ordered list of ranked restaurants plus the
/// echoed match/sort keys that produced it. Order is significant — it is the final ranked,
/// filtered list (explicit sort dominates; coverage is only a tie-breaker — invariant #5).
/// </summary>
public sealed record RecommendationResultDto(
    string Match,
    string Sort,
    IReadOnlyList<RecommendedRestaurantDto> Restaurants);

/// <summary>
/// One ranked restaurant in a recommendation result. Carries the representative figures
/// the engine computed (basket price, our smoothed rating, distance, coverage) so the
/// client can render them without a second call. Ratings/coords are always our own —
/// never Google (invariants #6/#7).
/// </summary>
public sealed record RecommendedRestaurantDto(
    Guid RestaurantId,
    string Name,
    decimal? BasketPriceAmount,
    string? BasketPriceCurrency,
    double? SmoothedRating,
    int RatingCount,
    double? DistanceKm,
    int Coverage);
