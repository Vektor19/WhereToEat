-- 0012_create_analytics_rollups.sql
-- Step 11: the analytics aggregation seam — a per-restaurant-per-hour rollup table.
--
-- This is the AGGREGATES-ONLY surface future B2B dashboards (7.5) read (invariant #11: venues see
-- only aggregates, never personal data). A row is one (restaurant, hour) bucket with impression /
-- card-open counts and the derived CTR. There is, by schema, NO actor hash, NO geohash, and NO
-- per-event id here — it cannot represent a person or a single event, only a count.
--
-- Step 11 implements the minimal rollup by grouping the append-only analytics.AnalyticsEvent store in
-- SQL on read (proving the path); this table is the materialized destination the Step 12 nightly job
-- will populate so dashboards read a ready aggregate instead of scanning raw events. Idempotent atop
-- 0000-0011 (the analytics schema is created in 0009).

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'analytics')
    EXEC (N'CREATE SCHEMA analytics');
GO

IF OBJECT_ID(N'analytics.RestaurantHourlyRollup', N'U') IS NULL
BEGIN
    CREATE TABLE analytics.RestaurantHourlyRollup
    (
        -- The restaurant the bucket aggregates (a venue id, never a person).
        RestaurantId UNIQUEIDENTIFIER NOT NULL,
        -- The hour-truncated bucket (UTC), matching analytics.AnalyticsEvent.OccurredAtHour.
        HourUtc      DATETIMEOFFSET   NOT NULL,
        -- How many times the restaurant's card was shown in results in this hour.
        Impressions  BIGINT           NOT NULL CONSTRAINT DF_RestaurantHourlyRollup_Impressions DEFAULT (0),
        -- How many times the restaurant's card was opened (details) in this hour.
        CardOpens    BIGINT           NOT NULL CONSTRAINT DF_RestaurantHourlyRollup_CardOpens DEFAULT (0),
        CONSTRAINT PK_RestaurantHourlyRollup PRIMARY KEY (RestaurantId, HourUtc)
    );
END
GO
