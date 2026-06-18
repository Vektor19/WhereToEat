-- 0007_create_protection_flags.sql
-- Step 5: the restaurant-level admin > parser protection (invariant #3).
--
-- The DoNotUpdate flag tells the parser to SKIP the whole venue so hand-curated data is never
-- overwritten — complementing the per-MenuItem DoNotParse flag from Step 3 (catalog.MenuItem). The
-- admin API host + EF CRUD that SET this flag arrive in Step 10; this is the table the Step 9 parser
-- persist gate reads. Lives in its own `admin` schema, keyed by the opaque venue Guid (no cross-module
-- FK — the Admin module stays independent of Catalog).

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'admin')
    EXEC (N'CREATE SCHEMA admin');
GO

CREATE TABLE admin.RestaurantProtection
(
    RestaurantId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_RestaurantProtection PRIMARY KEY,
    DoNotUpdate  BIT              NOT NULL CONSTRAINT DF_RestaurantProtection_DoNotUpdate DEFAULT (0)
);
GO
