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
}
