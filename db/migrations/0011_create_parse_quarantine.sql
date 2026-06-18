-- 0011_create_parse_quarantine.sql
-- Step 9: the parse quarantine / review queue (invariant #2 — unmappable parsed dishes are
-- QUARANTINED, never dropped, never fatal). When the normalizer cannot map a raw parsed dish name to
-- a canonical Catalog Dish, the raw item lands here for later admin resolution; it is NEVER written
-- as a live catalog.MenuItem until an admin maps it. The admin module surfaces this queue in a future
-- step. Lives in its own `parsing` schema, with no cross-module FK (the Parsing module stays
-- independent of Catalog — the only crossing is the resolved DishId, which is absent here precisely
-- because these rows could not be resolved).
--
-- DbUp applies scripts in numeric order; 0011 sits after the Step 4-5 catalog/admin scripts (0000-0009)
-- and the Step 7 price-median table (0010), and before the later steps' scripts.

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'parsing')
    EXEC (N'CREATE SCHEMA parsing');
GO

CREATE TABLE parsing.ParseQuarantine
(
    -- The quarantine item id (assigned in the domain ParseQuarantineItem.ForUnmapped factory).
    Id                     UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ParseQuarantine PRIMARY KEY,

    -- The source restaurant's natural key (name + address line) so an admin can re-attach the item to
    -- the right venue when they resolve the mapping.
    RestaurantName         NVARCHAR(400)    NOT NULL,
    RestaurantAddressLine  NVARCHAR(800)    NOT NULL,

    -- The raw parsed dish exactly as it appeared on the page (the unmappable name), plus its parsed
    -- price + currency and the optional weight / page category hint.
    RawName                NVARCHAR(400)    NOT NULL,
    PriceAmount            DECIMAL(18, 2)   NOT NULL CONSTRAINT CK_ParseQuarantine_AmountNonNeg CHECK (PriceAmount >= 0),
    PriceCurrency          CHAR(3)          NOT NULL,
    Weight                 NVARCHAR(100)    NULL,
    CategoryHint           NVARCHAR(200)    NULL,

    -- When the parser detected the unmappable item (UTC).
    DetectedAtUtc          DATETIMEOFFSET   NOT NULL
);
GO

-- The admin review queue is read newest-first per venue; index the detection time for that scan.
CREATE INDEX IX_ParseQuarantine_DetectedAt
    ON parsing.ParseQuarantine (DetectedAtUtc DESC);
GO
