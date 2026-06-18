-- 0003_create_menuitem.sql
-- Step 4: the MenuItem — the dish-at-restaurant join (one dish offered at one restaurant).
--
-- Carries the Money price (amount + ISO currency), an optional free-form weight/quantity,
-- the SourceKind provenance enum (0=Parsed draft, 1=Admin source-of-truth — invariant #3),
-- and the per-item DoNotParse flag that shields an admin-curated item from the parser
-- (invariant #3, admin > parser). A low-churn raw-parsed snapshot lives in a JSON column.
--
-- The one-menu-item-per-dish invariant (a restaurant cannot have two items for the same
-- dish) is enforced both by the domain aggregate and by a UNIQUE(RestaurantId, DishId) here.

CREATE TABLE catalog.MenuItem
(
    Id            UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_MenuItem PRIMARY KEY,
    RestaurantId  UNIQUEIDENTIFIER NOT NULL,
    DishId        UNIQUEIDENTIFIER NOT NULL,

    -- Money value object: non-negative amount + 3-letter ISO 4217 currency.
    PriceAmount   DECIMAL(18, 2)   NOT NULL CONSTRAINT CK_MenuItem_PriceNonNegative CHECK (PriceAmount >= 0),
    PriceCurrency CHAR(3)          NOT NULL,

    Weight        NVARCHAR(100)    NULL,

    -- Provenance + admin-protection flag (invariant #3).
    Source        INT              NOT NULL,
    DoNotParse    BIT              NOT NULL CONSTRAINT DF_MenuItem_DoNotParse DEFAULT (0),

    -- Low-churn raw-parsed snapshot (JSON). NULL until the parser fills it (later steps).
    RawSnapshot   NVARCHAR(MAX)    NULL
        CONSTRAINT CK_MenuItem_RawSnapshot_Json CHECK (RawSnapshot IS NULL OR ISJSON(RawSnapshot) = 1),

    CONSTRAINT FK_MenuItem_Restaurant FOREIGN KEY (RestaurantId)
        REFERENCES catalog.Restaurant (Id) ON DELETE CASCADE,
    CONSTRAINT FK_MenuItem_Dish FOREIGN KEY (DishId) REFERENCES catalog.Dish (Id),
    CONSTRAINT UQ_MenuItem_Restaurant_Dish UNIQUE (RestaurantId, DishId)
);
GO

-- Read the menu for a restaurant (restaurant details — §5.8) and find restaurants by dish
-- (recommendation candidate selection — §6 Step 1).
CREATE INDEX IX_MenuItem_RestaurantId ON catalog.MenuItem (RestaurantId);
GO
CREATE INDEX IX_MenuItem_DishId ON catalog.MenuItem (DishId);
GO
