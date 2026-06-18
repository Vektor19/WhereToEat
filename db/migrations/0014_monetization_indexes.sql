-- 0014_monetization_indexes.sql
-- Step 14: supporting indexes for the Monetization module (future-facing; no real billing).
--
-- Step 5's 0008 already created the monetization schema + tables (VerifiedStatus, AdPlacement,
-- Promotion), the VenueId indexes, and the window CHECK constraints. This script adds only what
-- Step 14's serving/read paths need that 0008 did not already cover, and stays IDEMPOTENT atop it.
--
-- CRITICAL (invariant #10): there is intentionally NO rank/boost/weight/priority/score column on any
-- monetization table, so no index here can turn an ad slot into an organic-ranking input. The
-- targeting-key index below only helps decide WHERE a labeled slot is shown (its audience), never how
-- organic results rank — the recommendation pipeline never joins these tables.

-- Serve labeled ad slots by targeting key within their active window (audience selection only). Guarded
-- so re-running the migration is a no-op (the DbUp journal already skips applied scripts; the IF guard
-- is belt-and-braces in case this script is run against a partially-set-up schema).
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_AdPlacement_TargetingKey_Window'
      AND object_id = OBJECT_ID(N'monetization.AdPlacement'))
BEGIN
    CREATE INDEX IX_AdPlacement_TargetingKey_Window
        ON monetization.AdPlacement (TargetingKey, StartsAt, EndsAt);
END
GO
