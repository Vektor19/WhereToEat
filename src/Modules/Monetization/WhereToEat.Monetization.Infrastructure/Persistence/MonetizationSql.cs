namespace WhereToEat.Monetization.Infrastructure.Persistence;

/// <summary>
/// The hand-written SQL for the Monetization data layer, kept in one place (named constants) so the
/// queries are reviewable as a unit. They sit over the SQL-script-owned <c>monetization</c> schema
/// (Step 5's <c>0008</c> tables + Step 14's <c>0014</c> indexes). CRITICAL (invariant #10): there is
/// no rank/boost/weight/priority/score column on either table, so no query here can make an ad slot an
/// organic-ranking input. All statements are parameterised. Dapper only — no EF.
/// </summary>
internal static class MonetizationSql
{
    // The per-venue Verified status: a single keyed lookup (the grant/revoke use-cases read it first).
    internal const string SelectVerifiedStatusByVenue =
        """
        SELECT VenueId, Tier
        FROM monetization.VerifiedStatus
        WHERE VenueId = @VenueId;
        """;

    // Idempotent upsert of the per-venue Verified status (grant/revoke both persist through this).
    internal const string UpsertVerifiedStatus =
        """
        MERGE monetization.VerifiedStatus AS target
        USING (SELECT @VenueId AS VenueId) AS source
            ON target.VenueId = source.VenueId
        WHEN MATCHED THEN
            UPDATE SET Tier = @Tier
        WHEN NOT MATCHED THEN
            INSERT (VenueId, Tier)
            VALUES (@VenueId, @Tier);
        """;

    // Inserts a newly created labeled ad placement. There is deliberately no UPDATE path that could
    // mutate a slot into a ranking signal — placements are a marked slot, never an organic input.
    internal const string InsertAdPlacement =
        """
        INSERT INTO monetization.AdPlacement (Id, VenueId, TargetingKey, StartsAt, EndsAt)
        VALUES (@Id, @VenueId, @TargetingKey, @StartsAt, @EndsAt);
        """;

    // Loads a single ad placement by id (used to confirm a created placement / for admin read-back).
    internal const string SelectAdPlacementById =
        """
        SELECT Id, VenueId, TargetingKey, StartsAt, EndsAt
        FROM monetization.AdPlacement
        WHERE Id = @Id;
        """;
}
