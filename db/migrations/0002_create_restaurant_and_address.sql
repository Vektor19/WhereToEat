-- 0002_create_restaurant_and_address.sql
-- Step 4: the Restaurant aggregate root plus its owned Address and ContactLinks.
--
-- Geo (invariant #7): coordinates are stored as a SQL Server `geography` column (Point,
-- SRID 4326 / WGS-84) sourced from OSM/Nominatim — NEVER raw Google lat/lng (there is no
-- raw lat/lng column at all). The `geography` column drives a SPATIAL INDEX used only to
-- *narrow* candidates within a radius; exact distance/ordering is decided in code by
-- Haversine. The optional Google Place ID and Maps deep-link (non-coordinate identifiers,
-- allowed to store) sit in plain columns next to it.
--
-- A low-churn raw-parsed snapshot is kept in an NVARCHAR(MAX) JSON column (the design's
-- "JSON for low-churn" note) so the parser can stash the source payload without a wide
-- relational shape.

CREATE TABLE catalog.Restaurant
(
    Id           UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Restaurant PRIMARY KEY,
    Name         NVARCHAR(300)    NOT NULL,

    -- Address (owned value object): a normalized line + optional city (geocoding takes a
    -- free-form string, so over-structuring adds no query value — matches Address.cs).
    AddressLine  NVARCHAR(500)    NOT NULL,
    AddressCity  NVARCHAR(200)    NULL,

    -- OSM-sourced spatial point (WGS-84 / SRID 4326). NULL until the restaurant is geocoded
    -- (Step 8). This is the ONLY coordinate storage — no Google lat/lng column exists.
    Location     GEOGRAPHY        NULL,

    -- Non-coordinate Google identifiers (allowed to store): Place ID + Maps deep-link.
    PlaceId      NVARCHAR(200)    NULL,
    MapsDeepLink NVARCHAR(1000)   NULL,

    -- Low-churn raw-parsed snapshot (JSON). NULL until the parser fills it (later steps).
    RawSnapshot  NVARCHAR(MAX)    NULL
        CONSTRAINT CK_Restaurant_RawSnapshot_Json CHECK (RawSnapshot IS NULL OR ISJSON(RawSnapshot) = 1)
);
GO

-- Index-assisted radius pre-filter (invariant #7): the spatial index lets
-- Location.STDistance(@p) / STIntersects narrow candidates cheaply before Haversine decides.
-- A bounding box is required for a GEOGRAPHY auto-grid spatial index in SQL Server.
CREATE SPATIAL INDEX SPATIAL_Restaurant_Location
    ON catalog.Restaurant (Location)
    USING GEOGRAPHY_AUTO_GRID;
GO

-- ContactLinks (owned value objects): website / social / phone, always shown for free and
-- never monetized (invariant #10) — so no "paid"/"promoted" column exists by design.
-- Kind is the ContactLinkKind enum's int value (0=Website, 1=Social, 2=Phone).
CREATE TABLE catalog.ContactLink
(
    Id           INT IDENTITY (1, 1) NOT NULL CONSTRAINT PK_ContactLink PRIMARY KEY,
    RestaurantId UNIQUEIDENTIFIER    NOT NULL,
    Kind         INT                 NOT NULL,
    Url          NVARCHAR(1000)      NOT NULL,
    Label        NVARCHAR(200)       NULL,
    CONSTRAINT FK_ContactLink_Restaurant FOREIGN KEY (RestaurantId)
        REFERENCES catalog.Restaurant (Id) ON DELETE CASCADE,
    -- Equality of a contact link is by kind + URL (matches ContactLink.cs); enforce it so the
    -- same link is never stored twice on a restaurant.
    CONSTRAINT UQ_ContactLink_Restaurant_Kind_Url UNIQUE (RestaurantId, Kind, Url)
);
GO

CREATE INDEX IX_ContactLink_RestaurantId ON catalog.ContactLink (RestaurantId);
GO
