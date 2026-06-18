namespace WhereToEat.Recommendation.Infrastructure.Medians;

/// <summary>
/// The hand-written SQL for the Step 12 nightly median <b>write</b> path (the read path's lookups live
/// in <see cref="RecommendationSql"/>). Kept as named constants in one place so the recompute query is
/// reviewable as a unit; all statements are parameterised. The recompute is a read of the current
/// priced items + a transactional replace of the median table — never a per-request aggregation.
/// </summary>
internal static class PriceMedianSql
{
    // Read every priced menu item with its restaurant's stored coordinates (Lat/Long via the geography
    // accessors — NULL until geocoded) and its dish's category, so the calculator can group by area
    // and by both taxonomy levels. The radius/spatial index is irrelevant here (a full scan of the
    // current menu is the intended nightly cost).
    internal const string SelectPricedMenuItems =
        """
        SELECT
            r.Location.Lat  AS Latitude,
            r.Location.Long AS Longitude,
            mi.DishId       AS DishId,
            d.CategoryId    AS CategoryId,
            mi.PriceAmount  AS PriceAmount,
            mi.PriceCurrency AS PriceCurrency
        FROM catalog.MenuItem mi
        INNER JOIN catalog.Dish d ON d.Id = mi.DishId
        INNER JOIN catalog.Restaurant r ON r.Id = mi.RestaurantId;
        """;

    // Replace the table contents: clear all rows, then insert the freshly computed snapshot. Wrapped in
    // a transaction by the writer so a concurrent reader sees either the old or the new full snapshot.
    internal const string DeleteAll = "DELETE FROM recommendation.PriceMedian;";

    internal const string Insert =
        """
        INSERT INTO recommendation.PriceMedian
            (AreaKey, CategoryId, DishId, MedianAmount, Currency, SampleSize)
        VALUES
            (@AreaKey, @CategoryId, @DishId, @MedianAmount, @Currency, @SampleSize);
        """;
}
