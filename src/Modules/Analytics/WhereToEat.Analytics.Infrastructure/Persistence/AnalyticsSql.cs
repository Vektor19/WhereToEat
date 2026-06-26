namespace WhereToEat.Analytics.Infrastructure.Persistence;

/// <summary>
/// The hand-written SQL for the analytics append path. Inserts are append-only (events are immutable
/// facts — no UPDATE/DELETE here). A multi-row VALUES insert is the batch path; the retained variable
/// dimensions ride in a JSON detail column so the relational shape stays narrow. All statements are
/// parameterised.
/// </summary>
internal static class AnalyticsSql
{
    internal const string InsertEvent =
        """
        INSERT INTO analytics.AnalyticsEvent
            (Id, Kind, OccurredAtHour, ActorHash, CoarseGeohash, DimensionsJson)
        VALUES
            (@Id, @Kind, @OccurredAtHour, @ActorHash, @CoarseGeohash, @DimensionsJson);
        """;

    // Read-back for the integration round-trip / a future minimal rollup (Step 11 owns the real
    // rollups). Selects the anonymized columns only — there is no raw-id / precise-coord column to
    // select, by schema.
    internal const string SelectEventById =
        """
        SELECT Id, Kind, OccurredAtHour, ActorHash, CoarseGeohash, DimensionsJson
        FROM analytics.AnalyticsEvent
        WHERE Id = @Id;
        """;

    // The Step 11 minimal rollup: impressions / card-opens / CTR per restaurant per hour. It groups the
    // append-only event store on read, projecting AGGREGATES ONLY (counts) — no actor hash, no geohash,
    // no per-event id leaves this query (invariant #11: venues see aggregates, never personal data). The
    // restaurant id is the retained dimension pulled from the JSON detail column (camelCase per the
    // Web serializer); rows with no restaurant id are excluded. Kind 0 = Impression, 2 = CardOpen.
    internal const string SelectRestaurantHourlyRollup =
        """
        SELECT
            CAST(JSON_VALUE(DimensionsJson, '$.restaurantId') AS UNIQUEIDENTIFIER) AS RestaurantId,
            OccurredAtHour AS HourUtc,
            SUM(CASE WHEN Kind = 0 THEN 1 ELSE 0 END) AS Impressions,
            SUM(CASE WHEN Kind = 2 THEN 1 ELSE 0 END) AS CardOpens
        FROM analytics.AnalyticsEvent
        WHERE OccurredAtHour >= @FromHourUtc
          AND OccurredAtHour <= @ToHourUtc
          AND JSON_VALUE(DimensionsJson, '$.restaurantId') IS NOT NULL
        GROUP BY
            CAST(JSON_VALUE(DimensionsJson, '$.restaurantId') AS UNIQUEIDENTIFIER),
            OccurredAtHour
        ORDER BY HourUtc, RestaurantId;
        """;

    // Step 23 §7.5 visibility/traffic: impressions / card-opens / action clicks for ONE restaurant over
    // the window, collapsed to a single aggregate row. Projects COUNTS ONLY — no actor hash, no geohash,
    // no per-event id leaves this query (invariant #11). Kind 0 = Impression, 2 = CardOpen, 3 = Action.
    // With no GROUP BY this is a scalar aggregate: it always returns exactly one row, and on an empty
    // match set SUM is NULL — so each column is COALESCE-zeroed at the SQL level (self-documenting) to
    // guarantee a non-null long, rather than relying on Dapper's NULL→default mapping.
    internal const string SelectRestaurantTraffic =
        """
        SELECT
            COALESCE(SUM(CASE WHEN Kind = 0 THEN 1 ELSE 0 END), 0) AS Impressions,
            COALESCE(SUM(CASE WHEN Kind = 2 THEN 1 ELSE 0 END), 0) AS CardOpens,
            COALESCE(SUM(CASE WHEN Kind = 3 THEN 1 ELSE 0 END), 0) AS Actions
        FROM analytics.AnalyticsEvent
        WHERE OccurredAtHour >= @FromHourUtc
          AND OccurredAtHour <= @ToHourUtc
          AND CAST(JSON_VALUE(DimensionsJson, '$.restaurantId') AS UNIQUEIDENTIFIER) = @RestaurantId;
        """;

    // Step 23 §7.5 demand by CATEGORY: how many Search events (Kind 4) referenced each category id in the
    // window. The retained category ids ride as a JSON array on the search dimensions, so the row is
    // cross-applied (OPENJSON) and grouped by the selection id — a taxonomy id, never a person. AGGREGATE
    // COUNTS ONLY; capped to the top-N most-searched.
    internal const string SelectDemandByCategory =
        """
        SELECT TOP (@Top)
            CAST(c.[value] AS UNIQUEIDENTIFIER) AS SelectionId,
            COUNT(*) AS SearchCount
        FROM analytics.AnalyticsEvent e
        CROSS APPLY OPENJSON(e.DimensionsJson, '$.categoryIds') c
        WHERE e.Kind = 4
          AND e.OccurredAtHour >= @FromHourUtc
          AND e.OccurredAtHour <= @ToHourUtc
          AND ISJSON(JSON_QUERY(e.DimensionsJson, '$.categoryIds')) = 1
        GROUP BY CAST(c.[value] AS UNIQUEIDENTIFIER)
        ORDER BY SearchCount DESC, SelectionId;
        """;

    // Step 23 §7.5 demand by DISH: the dish-level twin of the category query. Search events (Kind 4),
    // dish ids cross-applied from the JSON array, grouped + capped. AGGREGATE COUNTS ONLY.
    internal const string SelectDemandByDish =
        """
        SELECT TOP (@Top)
            CAST(d.[value] AS UNIQUEIDENTIFIER) AS SelectionId,
            COUNT(*) AS SearchCount
        FROM analytics.AnalyticsEvent e
        CROSS APPLY OPENJSON(e.DimensionsJson, '$.dishIds') d
        WHERE e.Kind = 4
          AND e.OccurredAtHour >= @FromHourUtc
          AND e.OccurredAtHour <= @ToHourUtc
          AND ISJSON(JSON_QUERY(e.DimensionsJson, '$.dishIds')) = 1
        GROUP BY CAST(d.[value] AS UNIQUEIDENTIFIER)
        ORDER BY SearchCount DESC, SelectionId;
        """;

    // Step 23 §7.5 price positioning: each dish a restaurant carries, with the venue's own price and the
    // area/category median the engine already materializes (recommendation.PriceMedian). This reads the
    // catalog menu + the precomputed market median ONLY — there is NO analytics-event, actor, or per-user
    // data here at all. The city-wide fallback median row (AreaKey = '*') is used per the §6 fallback.
    internal const string SelectPricePositioning =
        """
        SELECT
            mi.DishId        AS DishId,
            mi.PriceAmount   AS VenuePrice,
            pm.MedianAmount  AS MedianPrice,
            mi.PriceCurrency AS Currency
        FROM catalog.MenuItem mi
        OUTER APPLY (
            SELECT TOP 1 p.MedianAmount
            FROM recommendation.PriceMedian p
            WHERE p.DishId = mi.DishId
            ORDER BY CASE WHEN p.AreaKey = '*' THEN 1 ELSE 0 END, p.SampleSize DESC
        ) pm
        WHERE mi.RestaurantId = @RestaurantId
        ORDER BY mi.DishId;
        """;

    // Step 23 §7.5 ratings distribution: the cumulative all-time count + score sum from the materialized
    // ratings.RatingAggregate rollup (invariant #6). One keyed lookup — never a per-user raw Rating row.
    internal const string SelectRatingsDistribution =
        """
        SELECT ScoreCount, ScoreSum
        FROM ratings.RatingAggregate
        WHERE RestaurantId = @RestaurantId;
        """;
}
