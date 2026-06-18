-- 0010_create_price_medians.sql
-- Step 7: the precomputed per-area / per-category price-median table that f_price reads
-- (CLAUDE.md §6). This table is CREATED here (Step 7) so the DB-only DbPriceMedianProvider and the
-- Step 7 integration test can read/seed it independently of any background job. Step 12's nightly
-- job only POPULATES/REFRESHES its rows; it does not create it.
--
-- The recommendation engine never computes a median per request — it reads a ready value from this
-- table. f_price compares a venue's basket against the median for its area + selection, falling back
-- to the city-wide median when the per-area sample size is below the configurable threshold N. The
-- fallback row is the same shape with AreaKey = the city-wide sentinel ('*').
--
-- No Redis is involved at this step: Step 13 adds Redis transparently as a caching decorator over
-- the IPriceMedianProvider port (cache-then-DB), without changing this table or its readers.

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'recommendation')
    EXEC (N'CREATE SCHEMA recommendation');
GO

CREATE TABLE recommendation.PriceMedian
(
    -- The coarse area key the median is scoped to: a low-precision geohash of the area, or the
    -- city-wide sentinel '*' for the fallback row (used when a finer area's sample size is below N).
    AreaKey      VARCHAR(12)      NOT NULL,

    -- The selection the median is for: a category id OR a dish id (the two-level taxonomy,
    -- invariant #2). Exactly one is populated; the other is NULL. A pair of filtered unique indexes
    -- below enforces "exactly one, no duplicate area+selection".
    CategoryId   UNIQUEIDENTIFIER NULL,
    DishId       UNIQUEIDENTIFIER NULL,

    -- The precomputed median basket amount + its ISO currency, and the sample size behind it (the
    -- count the nightly job used; the threshold-N decision is encoded by whether a per-area row
    -- exists at all — when too few samples, the job writes only the city-wide row).
    MedianAmount DECIMAL(18, 2)   NOT NULL CONSTRAINT CK_PriceMedian_AmountNonNeg CHECK (MedianAmount >= 0),
    Currency     CHAR(3)          NOT NULL,
    SampleSize   INT              NOT NULL CONSTRAINT CK_PriceMedian_SampleNonNeg CHECK (SampleSize >= 0),

    -- Exactly one of CategoryId / DishId is set (the two-level taxonomy: a median is per category
    -- OR per dish, never both, never neither).
    CONSTRAINT CK_PriceMedian_OneSelection CHECK (
        (CategoryId IS NOT NULL AND DishId IS NULL)
        OR (CategoryId IS NULL AND DishId IS NOT NULL))
);
GO

-- One median per (area, dish); filtered so it applies only to dish rows. The Step 12 job upserts
-- against this key; the DB-only provider reads by it.
CREATE UNIQUE INDEX UX_PriceMedian_Area_Dish
    ON recommendation.PriceMedian (AreaKey, DishId)
    WHERE DishId IS NOT NULL;
GO

-- One median per (area, category); filtered to category rows.
CREATE UNIQUE INDEX UX_PriceMedian_Area_Category
    ON recommendation.PriceMedian (AreaKey, CategoryId)
    WHERE CategoryId IS NOT NULL;
GO
