-- 0001_create_taxonomy.sql
-- Step 4: the two-level taxonomy tables (CLAUDE.md invariant #2).
-- Category = a GROUP of dishes (Перші страви, Фаст-фуд…); Dish = a concrete position
-- inside exactly one Category (Борщ, Піца Маргарита…). A dish references its category;
-- the FK + NOT NULL enforce "a dish always belongs to a category" at the DB level too.
--
-- These SQL scripts are the schema-of-record (DbUp applies them in numeric order, once
-- each, tracked in a journal table). Ids are GUIDs to match the domain's strongly-typed
-- ids; we use NONCLUSTERED PK on the GUID + a clustered surrogate-free design is avoided
-- here for simplicity (GUID PK is fine at this catalog scale).

CREATE TABLE catalog.Category
(
    Id   UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Category PRIMARY KEY,
    Name NVARCHAR(200)    NOT NULL
);
GO

CREATE TABLE catalog.Dish
(
    Id            UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Dish PRIMARY KEY,
    CategoryId    UNIQUEIDENTIFIER NOT NULL,
    CanonicalName NVARCHAR(300)    NOT NULL,
    CONSTRAINT FK_Dish_Category FOREIGN KEY (CategoryId) REFERENCES catalog.Category (Id)
);
GO

-- Dishes are looked up by category (list dishes within a category — §5.4), so index the FK.
CREATE INDEX IX_Dish_CategoryId ON catalog.Dish (CategoryId);
GO
