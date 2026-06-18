-- 0004_create_ratings.sql
-- Step 5: our own, cumulative all-time ratings (invariant #6).
--
-- Two tables: the raw per-user Rating (the fact a user scored a venue), and the per-restaurant
-- RatingAggregate (the cumulative sum + count). The Bayesian additive-prior smoothed value the
-- ranking consumes is NOT stored — it is pure math over ScoreSum/ScoreCount (BayesianSmoothing),
-- recomputed wherever needed, so the materialized rollup always matches the formula.
--
-- A restaurant/user is referenced here only by its opaque Guid (no PII — invariant #11), the same
-- Guid the Catalog/Users modules use, without a cross-module FK (the modules stay independent).

-- CREATE SCHEMA must be the first statement in its batch, so it sits alone (no GO needed before it).
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'ratings')
    EXEC (N'CREATE SCHEMA ratings');
GO

CREATE TABLE ratings.Rating
(
    Id           UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Rating PRIMARY KEY,
    RestaurantId UNIQUEIDENTIFIER NOT NULL,
    UserId       UNIQUEIDENTIFIER NOT NULL,
    Score        INT              NOT NULL CONSTRAINT CK_Rating_Score CHECK (Score BETWEEN 1 AND 5),
    GivenAt      DATETIMEOFFSET   NOT NULL,
    -- A user has at most one rating per restaurant (revising updates it, never duplicates).
    CONSTRAINT UQ_Rating_Restaurant_User UNIQUE (RestaurantId, UserId)
);
GO

-- Find a restaurant's raw ratings (recompute job) and a user's ratings (revise path).
CREATE INDEX IX_Rating_RestaurantId ON ratings.Rating (RestaurantId);
GO

-- The hot read path: the per-restaurant cumulative rollup the recommendation candidate source joins.
-- Keyed by RestaurantId so the smoothed value is a single keyed lookup.
CREATE TABLE ratings.RatingAggregate
(
    RestaurantId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_RatingAggregate PRIMARY KEY,
    -- All-time sum of raw scores and the count of those scores (the two BayesianSmoothing inputs).
    ScoreSum     FLOAT            NOT NULL CONSTRAINT CK_RatingAggregate_SumNonNeg CHECK (ScoreSum >= 0),
    ScoreCount   BIGINT           NOT NULL CONSTRAINT CK_RatingAggregate_CountNonNeg CHECK (ScoreCount >= 0)
);
GO
