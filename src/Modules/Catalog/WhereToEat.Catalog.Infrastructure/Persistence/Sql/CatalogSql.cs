namespace WhereToEat.Catalog.Infrastructure.Persistence.Sql;

/// <summary>
/// The hand-written SQL for the catalog hot paths, kept in one place (named constants) so the
/// queries are reviewable as a unit and the repository reads like orchestration. All statements
/// are parameterised; the <c>geography</c> column is read/written via WKT
/// (<c>geography::STGeomFromText(@Wkt, 4326)</c> / <c>.STAsText()</c>) so no native spatial interop
/// is required. Coordinates are OSM-sourced only — there is no Google lat/lng column (invariant #7).
/// </summary>
internal static class CatalogSql
{
    // ---- Category -------------------------------------------------------------------------

    internal const string InsertCategory =
        """
        INSERT INTO catalog.Category (Id, Name)
        VALUES (@Id, @Name);
        """;

    internal const string SelectCategoryById =
        """
        SELECT Id, Name
        FROM catalog.Category
        WHERE Id = @Id;
        """;

    // Deterministic taxonomy list (invariant #1): the user picks from this, no free-text search.
    internal const string SelectAllCategories =
        """
        SELECT Id, Name
        FROM catalog.Category
        ORDER BY Name;
        """;

    // Prefix filter for the selection set — a LIKE '<prefix>%' anchored at the start, not NLP
    // (invariant #1). @Prefix is supplied already-escaped for LIKE wildcards; TOP caps the result.
    internal const string SearchCategoriesByPrefix =
        """
        SELECT TOP (@Limit) Id, Name
        FROM catalog.Category
        WHERE Name LIKE @Prefix + N'%' ESCAPE N'\'
        ORDER BY Name;
        """;

    // ---- Dish -----------------------------------------------------------------------------

    internal const string InsertDish =
        """
        INSERT INTO catalog.Dish (Id, CategoryId, CanonicalName)
        VALUES (@Id, @CategoryId, @CanonicalName);
        """;

    internal const string SelectDishById =
        """
        SELECT Id, CategoryId, CanonicalName
        FROM catalog.Dish
        WHERE Id = @Id;
        """;

    // "List dishes within a category" (§5.4) — uses IX_Dish_CategoryId; ordered for a stable list.
    internal const string SelectDishesByCategory =
        """
        SELECT Id, CategoryId, CanonicalName
        FROM catalog.Dish
        WHERE CategoryId = @CategoryId
        ORDER BY CanonicalName;
        """;

    // Prefix filter to add a concrete dish to the selection — LIKE '<prefix>%', not NLP (invariant #1).
    internal const string SearchDishesByPrefix =
        """
        SELECT TOP (@Limit) Id, CategoryId, CanonicalName
        FROM catalog.Dish
        WHERE CanonicalName LIKE @Prefix + N'%' ESCAPE N'\'
        ORDER BY CanonicalName;
        """;

    // ---- Restaurant (aggregate) -----------------------------------------------------------

    // Latitude/Longitude come back from the geography point so the row maps straight onto our
    // GeoPoint value object; @Wkt is "POINT(lon lat)" (WKT is longitude-first).
    internal const string InsertRestaurant =
        """
        INSERT INTO catalog.Restaurant (Id, Name, AddressLine, AddressCity, Location, PlaceId, MapsDeepLink)
        VALUES (
            @Id,
            @Name,
            @AddressLine,
            @AddressCity,
            CASE WHEN @Wkt IS NULL THEN NULL ELSE geography::STGeomFromText(@Wkt, 4326) END,
            @PlaceId,
            @MapsDeepLink);
        """;

    internal const string UpdateRestaurant =
        """
        UPDATE catalog.Restaurant
        SET Name = @Name,
            AddressLine = @AddressLine,
            AddressCity = @AddressCity,
            Location = CASE WHEN @Wkt IS NULL THEN NULL ELSE geography::STGeomFromText(@Wkt, 4326) END,
            PlaceId = @PlaceId,
            MapsDeepLink = @MapsDeepLink
        WHERE Id = @Id;
        """;

    internal const string SelectRestaurantById =
        """
        SELECT
            Id,
            Name,
            AddressLine,
            AddressCity,
            Location.Lat        AS Latitude,
            Location.Long       AS Longitude,
            PlaceId,
            MapsDeepLink
        FROM catalog.Restaurant
        WHERE Id = @Id;
        """;

    // The Step 12 geocode-refresh job's queued work: restaurants that have an address but have NOT been
    // geocoded yet (Location IS NULL). Ordered by Id so a capped batch is deterministic across runs;
    // a venue that gets geocoded simply drops out of the next batch (the job is idempotent).
    internal const string SelectRestaurantIdsNeedingGeocode =
        """
        SELECT TOP (@MaxCount) Id
        FROM catalog.Restaurant
        WHERE Location IS NULL
          AND AddressLine IS NOT NULL
          AND LTRIM(RTRIM(AddressLine)) <> N''
        ORDER BY Id;
        """;

    internal const string SelectMenuItemsByRestaurant =
        """
        SELECT Id, DishId, PriceAmount, PriceCurrency, Weight, Source, DoNotParse
        FROM catalog.MenuItem
        WHERE RestaurantId = @RestaurantId;
        """;

    internal const string SelectContactLinksByRestaurant =
        """
        SELECT Kind, Url, Label
        FROM catalog.ContactLink
        WHERE RestaurantId = @RestaurantId;
        """;

    internal const string InsertMenuItem =
        """
        INSERT INTO catalog.MenuItem
            (Id, RestaurantId, DishId, PriceAmount, PriceCurrency, Weight, Source, DoNotParse)
        VALUES
            (@Id, @RestaurantId, @DishId, @PriceAmount, @PriceCurrency, @Weight, @Source, @DoNotParse);
        """;

    internal const string DeleteMenuItemsByRestaurant =
        """
        DELETE FROM catalog.MenuItem WHERE RestaurantId = @RestaurantId;
        """;

    internal const string InsertContactLink =
        """
        INSERT INTO catalog.ContactLink (RestaurantId, Kind, Url, Label)
        VALUES (@RestaurantId, @Kind, @Url, @Label);
        """;

    internal const string DeleteContactLinksByRestaurant =
        """
        DELETE FROM catalog.ContactLink WHERE RestaurantId = @RestaurantId;
        """;

    // ---- Radius pre-filter (invariant #7) -------------------------------------------------

    // Index-assisted candidate narrowing: the spatial index serves STDistance against the query
    // point, returning only restaurants whose stored geography lies within @RadiusMeters. Exact
    // distance/ordering is then computed in code via Haversine — "the index narrows, Haversine
    // decides". @Wkt is the user's location as "POINT(lon lat)".
    internal const string SelectRestaurantsWithinRadius =
        """
        DECLARE @origin GEOGRAPHY = geography::STGeomFromText(@Wkt, 4326);
        SELECT
            Id,
            Name,
            AddressLine,
            AddressCity,
            Location.Lat  AS Latitude,
            Location.Long AS Longitude,
            PlaceId,
            MapsDeepLink
        FROM catalog.Restaurant
        WHERE Location IS NOT NULL
          AND Location.STDistance(@origin) <= @RadiusMeters;
        """;
}
