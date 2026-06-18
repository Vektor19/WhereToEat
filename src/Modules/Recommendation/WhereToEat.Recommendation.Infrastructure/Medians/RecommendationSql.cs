namespace WhereToEat.Recommendation.Infrastructure.Medians;

/// <summary>
/// The hand-written SQL for the recommendation read paths, kept in one place (named constants) so
/// the queries are reviewable as a unit. All statements are parameterised. The median read is a
/// single keyed lookup of a <b>precomputed</b> value (no per-request aggregation — CLAUDE.md §6
/// <c>f_price</c>); the candidate query joins the catalog read model, the radius pre-filter, and the
/// materialized rating aggregate by id (a DB join — never a code reference into <c>Ratings.Domain</c>).
/// </summary>
internal static class RecommendationSql
{
    /// <summary>The city-wide fallback area sentinel (matches the Step 12 writer / migration 0010).</summary>
    public const string CityWideAreaKey = "*";

    // Reads the precomputed median for a selection in an area, preferring the per-area row and
    // falling back to the city-wide ('*') row — the threshold-N fallback is encoded by whether a
    // per-area row was written at all (the nightly job only writes a per-area row above N samples).
    // ORDER BY puts the per-area row first; TOP(1) takes it, else the city-wide row.
    internal const string SelectDishMedian =
        """
        SELECT TOP (1) MedianAmount
        FROM recommendation.PriceMedian
        WHERE DishId = @DishId AND AreaKey IN (@AreaKey, @CityWideAreaKey)
        ORDER BY CASE WHEN AreaKey = @AreaKey THEN 0 ELSE 1 END;
        """;

    internal const string SelectCategoryMedian =
        """
        SELECT TOP (1) MedianAmount
        FROM recommendation.PriceMedian
        WHERE CategoryId = @CategoryId AND AreaKey IN (@AreaKey, @CityWideAreaKey)
        ORDER BY CASE WHEN AreaKey = @AreaKey THEN 0 ELSE 1 END;
        """;

    // The candidate query: for the selected dish/category ids, find restaurants whose menu has at
    // least one matching item, returning the cheapest matched price per (restaurant, selection) and
    // the venue's materialized smoothed-rating inputs (sum/count) LEFT JOINed from the ratings
    // rollup by RestaurantId. The radius pre-filter (when a location is supplied) uses the spatial
    // index; exact distance is decided in code via Haversine (invariant #7). The smoothed rating is
    // computed in code from the joined sum/count — a DB join carries the inputs, NOT a Ratings.Domain
    // reference. @DishIds / @CategoryIds are expanded by Dapper into IN (...) lists; an empty list is
    // passed a single impossible id so the IN never collapses to invalid SQL.
    internal const string SelectCandidates =
        """
        DECLARE @origin GEOGRAPHY = CASE WHEN @Wkt IS NULL THEN NULL ELSE geography::STGeomFromText(@Wkt, 4326) END;

        WITH matched AS (
            SELECT
                mi.RestaurantId,
                CAST(NULL AS UNIQUEIDENTIFIER) AS CategoryId,
                mi.DishId                      AS DishId,
                MIN(mi.PriceAmount)            AS PriceAmount,
                MIN(mi.PriceCurrency)          AS PriceCurrency
            FROM catalog.MenuItem mi
            WHERE mi.DishId IN @DishIds
            GROUP BY mi.RestaurantId, mi.DishId

            UNION ALL

            SELECT
                mi.RestaurantId,
                dish.CategoryId                AS CategoryId,
                CAST(NULL AS UNIQUEIDENTIFIER)  AS DishId,
                MIN(mi.PriceAmount)            AS PriceAmount,
                MIN(mi.PriceCurrency)          AS PriceCurrency
            FROM catalog.MenuItem mi
            INNER JOIN catalog.Dish dish ON dish.Id = mi.DishId
            WHERE dish.CategoryId IN @CategoryIds
            GROUP BY mi.RestaurantId, dish.CategoryId
        )
        SELECT
            r.Id            AS RestaurantId,
            r.Name          AS Name,
            r.Location.Lat  AS Latitude,
            r.Location.Long AS Longitude,
            agg.ScoreSum    AS ScoreSum,
            agg.ScoreCount  AS ScoreCount,
            m.CategoryId    AS CategoryId,
            m.DishId        AS DishId,
            m.PriceAmount   AS PriceAmount,
            m.PriceCurrency AS PriceCurrency
        FROM matched m
        INNER JOIN catalog.Restaurant r ON r.Id = m.RestaurantId
        LEFT JOIN ratings.RatingAggregate agg ON agg.RestaurantId = r.Id
        WHERE @origin IS NULL
           OR (r.Location IS NOT NULL AND r.Location.STDistance(@origin) <= @RadiusMeters);
        """;
}
