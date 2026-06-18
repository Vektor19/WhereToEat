-- 0009_create_analytics_events.sql
-- Step 5: the anonymized analytics event store (domain shape; ingest pipeline is Step 11).
--
-- Append-only, anonymized-by-schema (§8.1 / invariant #11): there is NO column for a raw user/session
-- id, NO precise lat/lng column, and the time is stored already hour-truncated. The only identity-ish
-- columns are the rotating-salted ActorHash (non-reversible) and a coarse neighbourhood-grade
-- CoarseGeohash. The variable retained dimensions (dish/category ids, sort mode, filters, position)
-- ride in a JSON detail column so the relational shape stays narrow and append-cheap.

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'analytics')
    EXEC (N'CREATE SCHEMA analytics');
GO

CREATE TABLE analytics.AnalyticsEvent
(
    Id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AnalyticsEvent PRIMARY KEY,
    -- EventKind enum (0 = Impression, 1 = View, 2 = CardOpen, 3 = Action, 4 = Search, 5 = Filter,
    -- 6 = RatingGiven, 7 = Session).
    Kind           INT              NOT NULL,
    -- Hour-truncated occurrence time (UTC). Minute/second precision is dropped at ingest.
    OccurredAtHour DATETIMEOFFSET   NOT NULL,
    -- Rotating-salted, non-reversible hash of the actor (NULL for actor-less events). Never the raw id.
    ActorHash      CHAR(64)         NULL,
    -- Coarse neighbourhood-grade geohash (NULL when no opt-in location). Never precise lat/lng.
    -- Capped at 6 chars to match the domain's Geohash.MaxPrecision: the schema enforces the same
    -- neighbourhood-grade coarseness, so a row written outside the domain can never store a finer
    -- (7-12 char) geohash that Geohash.Create() would reject on read.
    CoarseGeohash  VARCHAR(6)       NULL
        CONSTRAINT CK_AnalyticsEvent_CoarseGeohash_MaxPrecision CHECK (LEN(CoarseGeohash) <= 6),
    -- Retained non-identifying dimensions (dish/category ids, sort, filters, position).
    DimensionsJson NVARCHAR(MAX)    NOT NULL
        CONSTRAINT CK_AnalyticsEvent_Dimensions_Json CHECK (ISJSON(DimensionsJson) = 1)
);
GO

-- Demand/funnel rollups read by hour and by kind; an index supports the Step 11 minimal rollup.
CREATE INDEX IX_AnalyticsEvent_OccurredAtHour ON analytics.AnalyticsEvent (OccurredAtHour, Kind);
GO
