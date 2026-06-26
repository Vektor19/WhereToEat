namespace WhereToEat.Ratings.Infrastructure.Persistence;

/// <summary>
/// The hand-written SQL for the ratings hot path, kept in one place (named constants) so the queries
/// are reviewable as a unit. The per-restaurant aggregate read is a single keyed lookup; the upsert
/// uses <c>MERGE</c> so the Step 12 recompute job can rewrite the materialized rollup idempotently.
/// All statements are parameterised.
/// </summary>
internal static class RatingsSql
{
    internal const string SelectAggregateByRestaurant =
        """
        SELECT RestaurantId, ScoreSum, ScoreCount
        FROM ratings.RatingAggregate
        WHERE RestaurantId = @RestaurantId;
        """;

    // The Step 12 rating-recompute job's source read: the cumulative all-time sum + count of raw
    // scores per restaurant, straight from the per-user Rating facts (invariant #6 — our own, all-time
    // ratings). The job materializes these two numbers into RatingAggregate via UpsertAggregate; the
    // smoothed value the engine ranks on is then BayesianRatingSmoothing over them (single formula).
    internal const string SelectRawAggregatesGroupedByRestaurant =
        """
        SELECT
            RestaurantId,
            CAST(SUM(CAST(Score AS FLOAT)) AS FLOAT) AS ScoreSum,
            COUNT_BIG(*)                             AS ScoreCount
        FROM ratings.Rating
        GROUP BY RestaurantId;
        """;

    // Idempotent materialization (Step 12 recompute job): insert the rollup or overwrite the stored
    // sum/count if it already exists. The smoothed value is NOT stored — it is pure math over these
    // two columns (BayesianSmoothing), recomputed wherever it is needed.
    internal const string UpsertAggregate =
        """
        MERGE ratings.RatingAggregate AS target
        USING (SELECT @RestaurantId AS RestaurantId) AS source
            ON target.RestaurantId = source.RestaurantId
        WHEN MATCHED THEN
            UPDATE SET ScoreSum = @ScoreSum, ScoreCount = @ScoreCount
        WHEN NOT MATCHED THEN
            INSERT (RestaurantId, ScoreSum, ScoreCount)
            VALUES (@RestaurantId, @ScoreSum, @ScoreCount);
        """;

    // ---- Write side (Step 21): the raw per-user Rating fact behind IRatingRepository ----------------
    // The submit use-case looks up an existing rating (revise-vs-create) and then inserts or updates a
    // single row. The unique constraint UQ_Rating_Restaurant_User guarantees at most one row per
    // (RestaurantId, UserId) — revising updates that row, never adds a second (one rating per user per
    // restaurant, invariant #6). Only the opaque Guids + score + timestamp are stored (no PII, #11).

    // Find the row for one user's rating of one restaurant (the revise-vs-create decision input).
    internal const string SelectRatingByRestaurantAndUser =
        """
        SELECT Id, RestaurantId, UserId, Score, GivenAt
        FROM ratings.Rating
        WHERE RestaurantId = @RestaurantId AND UserId = @UserId;
        """;

    // Insert a brand-new rating fact (the user had none for this restaurant).
    internal const string InsertRating =
        """
        INSERT INTO ratings.Rating (Id, RestaurantId, UserId, Score, GivenAt)
        VALUES (@Id, @RestaurantId, @UserId, @Score, @GivenAt);
        """;

    // Revise an existing rating in place (same row, keyed by its Id): overwrite score + timestamp.
    internal const string UpdateRating =
        """
        UPDATE ratings.Rating
        SET Score = @Score, GivenAt = @GivenAt
        WHERE Id = @Id;
        """;
}
