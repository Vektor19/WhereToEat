-- 0000_create_schema.sql
-- Step 4: bootstrap the `catalog` SQL schema that every later catalog table lives in.
-- Kept as the lowest-numbered script so DbUp creates the schema before any CREATE TABLE
-- references it. CREATE SCHEMA must be the first statement in its own batch, so this is a
-- single-statement script (no GO needed).
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'catalog')
    EXEC (N'CREATE SCHEMA catalog');
