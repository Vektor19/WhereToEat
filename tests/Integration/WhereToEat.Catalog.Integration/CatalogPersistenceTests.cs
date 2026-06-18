using Dapper;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using WhereToEat.BuildingBlocks.Migrations;
using WhereToEat.BuildingBlocks.Persistence;
using WhereToEat.Catalog.Domain.Identifiers;
using WhereToEat.Catalog.Domain.Restaurants;
using WhereToEat.Catalog.Domain.Taxonomy;
using WhereToEat.Catalog.Infrastructure.Persistence;
using WhereToEat.SharedKernel.Geo;
using WhereToEat.SharedKernel.ValueObjects;
using Xunit;

namespace WhereToEat.Catalog.Integration;

/// <summary>
/// Step 4 containerized-SQL integration tests: (1) the migration runner creates the schema and the
/// geography spatial index (and re-running is a no-op); (2) the Dapper repository round-trips a
/// Restaurant + Dish + MenuItem aggregate with correct Money/Place ID/coordinates; (3) the radius
/// pre-filter returns only the in-radius candidates and Haversine orders them in code. All run
/// against a real SQL Server container (Docker required).
/// </summary>
public sealed class CatalogPersistenceTests : IClassFixture<SqlServerFixture>
{
    // Kyiv city centre (Maidan), used as the radius-test origin.
    private const double KyivLat = 50.4501;
    private const double KyivLon = 30.5234;

    private readonly SqlServerFixture _fixture;
    private readonly SqlConnectionFactory _connectionFactory;
    private readonly DapperCatalogRepository _repository;
    private readonly DapperCatalogSpatialReader _spatialReader;

    public CatalogPersistenceTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
        _connectionFactory = new SqlConnectionFactory(fixture.ConnectionString);
        _repository = new DapperCatalogRepository(_connectionFactory);
        _spatialReader = new DapperCatalogSpatialReader(_connectionFactory);
    }

    [Fact]
    public void Migrations_CreatedTheCatalogSchemaAndScripts()
    {
        // The fixture applied the scripts on startup; assert the expected versioned set ran.
        _fixture.AppliedScripts.Should().Contain(s => s.Contains("0000_create_schema"));
        _fixture.AppliedScripts.Should().Contain(s => s.Contains("0001_create_taxonomy"));
        _fixture.AppliedScripts.Should().Contain(s => s.Contains("0002_create_restaurant_and_address"));
        _fixture.AppliedScripts.Should().Contain(s => s.Contains("0003_create_menuitem"));
    }

    [Fact]
    public void Migrations_AreIdempotent_SecondRunAppliesNothing()
    {
        // Re-running against the already-migrated database is a no-op (DbUp journal).
        var rerun = MigrationRunner.Run(_fixture.ConnectionString);

        rerun.Successful.Should().BeTrue();
        rerun.ScriptsApplied.Should().BeEmpty("the journal table records the scripts already applied");
    }

    [Fact]
    public async Task Restaurant_CoordinateColumnIsGeography_WithASpatialIndex()
    {
        using var connection = (SqlConnection)await _connectionFactory.CreateOpenConnectionAsync();

        // The coordinate column's SQL type is `geography` (not a raw lat/lng pair).
        var columnType = await connection.QuerySingleAsync<string>(
            """
            SELECT t.name
            FROM sys.columns c
            JOIN sys.types t ON c.user_type_id = t.user_type_id
            WHERE c.object_id = OBJECT_ID('catalog.Restaurant') AND c.name = 'Location';
            """);
        columnType.Should().Be("geography");

        // A spatial index exists on the Restaurant table (index_type 4 = spatial).
        var spatialIndexCount = await connection.QuerySingleAsync<int>(
            """
            SELECT COUNT(*)
            FROM sys.indexes
            WHERE object_id = OBJECT_ID('catalog.Restaurant') AND type = 4;
            """);
        spatialIndexCount.Should().BeGreaterThan(0);

        // There is NO raw Google lat/lng column anywhere on the table (invariant #7).
        var rawCoordColumns = await connection.QuerySingleAsync<int>(
            """
            SELECT COUNT(*)
            FROM sys.columns
            WHERE object_id = OBJECT_ID('catalog.Restaurant')
              AND name IN ('Latitude', 'Longitude', 'Lat', 'Lng', 'GoogleLat', 'GoogleLng');
            """);
        rawCoordColumns.Should().Be(0);
    }

    [Fact]
    public async Task Repository_RoundTripsTheCatalogAggregate()
    {
        var (categoryId, dishId) = await SeedCategoryAndDishAsync("Перші страви", "Борщ");

        var address = Address.Create("вул. Хрещатик, 1", "Київ").Value;
        var restaurant = Restaurant.Create("Тестова Корчма", address).Value;

        var point = GeoPoint.Create(KyivLat, KyivLon).Value;
        var coordinates = Coordinates.FromOsmGeoPoint(point, placeId: "ChIJ-test-place-id", mapsDeepLink: "https://maps.app/test").Value;
        restaurant.SetCoordinates(coordinates);

        restaurant.AddContactLink(ContactLink.Create(ContactLinkKind.Website, "https://korchma.example", "Сайт").Value);

        var price = Money.Create(149.50m, "UAH").Value;
        restaurant.AddMenuItem(dishId, price, weight: "350 г", source: SourceKind.Admin, doNotParse: true);

        await _repository.AddRestaurantAsync(restaurant);

        var loaded = await _repository.GetRestaurantByIdAsync(restaurant.Id);

        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("Тестова Корчма");
        loaded.Address.Line.Should().Be("вул. Хрещатик, 1");
        loaded.Address.City.Should().Be("Київ");

        loaded.Coordinates.Should().NotBeNull();
        loaded.Coordinates!.PlaceId.Should().Be("ChIJ-test-place-id");
        loaded.Coordinates.MapsDeepLink.Should().Be("https://maps.app/test");
        loaded.Coordinates.Point.Latitude.Should().BeApproximately(KyivLat, 1e-5);
        loaded.Coordinates.Point.Longitude.Should().BeApproximately(KyivLon, 1e-5);

        loaded.ContactLinks.Should().ContainSingle()
            .Which.Url.Should().Be("https://korchma.example");

        loaded.MenuItems.Should().ContainSingle();
        var item = loaded.MenuItems.Single();
        item.DishId.Should().Be(dishId);
        item.Price.Amount.Should().Be(149.50m);
        item.Price.Currency.Should().Be("UAH");
        item.Weight.Should().Be("350 г");
        item.Source.Should().Be(SourceKind.Admin);
        item.DoNotParse.Should().BeTrue();

        // Rehydration must not surface stale domain events (the mapper clears them).
        loaded.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task Update_ReplacesChildren_HappyPath()
    {
        var (_, firstDishId) = await SeedCategoryAndDishAsync("Десерти", "Тірамісу");
        var (_, secondDishId) = await SeedCategoryAndDishAsync("Напої", "Кава");

        var address = Address.Create("вул. Стара, 2", "Львів").Value;
        var restaurant = Restaurant.Create("Кав'ярня", address).Value;
        restaurant.AddContactLink(ContactLink.Create(ContactLinkKind.Website, "https://old.example", "Старий сайт").Value);
        restaurant.AddMenuItem(firstDishId, Money.Create(80m, "UAH").Value);
        await _repository.AddRestaurantAsync(restaurant);

        // Mutate the SAME aggregate: rename, swap the menu item for a different dish, swap the link.
        var reloaded = await _repository.GetRestaurantByIdAsync(restaurant.Id);
        var updated = Restaurant.Create(reloaded!.Id, "Кав'ярня (оновлено)", Address.Create("вул. Нова, 9", "Львів").Value).Value;
        updated.AddMenuItem(secondDishId, Money.Create(45m, "UAH").Value);
        updated.AddContactLink(ContactLink.Create(ContactLinkKind.Social, "https://instagram.com/new", "Новий").Value);

        await _repository.UpdateRestaurantAsync(updated);

        var after = await _repository.GetRestaurantByIdAsync(restaurant.Id);
        after!.Name.Should().Be("Кав'ярня (оновлено)");
        after.Address.Line.Should().Be("вул. Нова, 9");
        after.MenuItems.Should().ContainSingle().Which.DishId.Should().Be(secondDishId);
        after.ContactLinks.Should().ContainSingle().Which.Url.Should().Be("https://instagram.com/new");
    }

    [Fact]
    public async Task Update_RollsBack_WhenAChildInsertFails_LeavingPriorChildrenIntact()
    {
        var (_, validDishId) = await SeedCategoryAndDishAsync("Перші страви", "Солянка");

        var address = Address.Create("вул. Захищена, 5", "Київ").Value;
        var restaurant = Restaurant.Create("Захищений заклад", address).Value;
        restaurant.AddContactLink(ContactLink.Create(ContactLinkKind.Website, "https://intact.example", "Сайт").Value);
        restaurant.AddMenuItem(validDishId, Money.Create(120m, "UAH").Value, weight: "300 г", source: SourceKind.Admin, doNotParse: true);
        await _repository.AddRestaurantAsync(restaurant);

        // Build an update whose menu item references a dish that does NOT exist in the DB, so the
        // child INSERT violates FK_MenuItem_Dish — but only AFTER the UPDATE + DELETEs have run.
        var orphanDishId = DishId.New();
        var corrupting = Restaurant.Create(restaurant.Id, "Не повинно зберегтись", Address.Create("вул. Привид, 0", "Київ").Value).Value;
        corrupting.AddMenuItem(orphanDishId, Money.Create(999m, "UAH").Value);

        // The failing child INSERT must surface, not be swallowed.
        await Assert.ThrowsAnyAsync<Exception>(() => _repository.UpdateRestaurantAsync(corrupting));

        // Atomicity: the prior aggregate is fully intact — header unchanged, original menu item and
        // contact link still present (the DELETEs rolled back with the failed INSERT).
        var after = await _repository.GetRestaurantByIdAsync(restaurant.Id);
        after.Should().NotBeNull();
        after!.Name.Should().Be("Захищений заклад", "the failed UPDATE rolled back");
        after.Address.Line.Should().Be("вул. Захищена, 5");
        after.MenuItems.Should().ContainSingle().Which.DishId.Should().Be(validDishId);
        after.MenuItems.Single().DoNotParse.Should().BeTrue();
        after.ContactLinks.Should().ContainSingle().Which.Url.Should().Be("https://intact.example");
    }

    [Fact]
    public async Task RadiusPreFilter_ReturnsOnlyInRadiusCandidates_OrderedByHaversine()
    {
        var (_, dishId) = await SeedCategoryAndDishAsync("Фаст-фуд", "Піца");

        // Near (~0 km), mid (~4 km north), and far (~600 km, Lviv) restaurants.
        var origin = GeoPoint.Create(KyivLat, KyivLon).Value;
        var nearId = await SeedRestaurantAtAsync("Near", KyivLat, KyivLon, dishId);
        var midId = await SeedRestaurantAtAsync("Mid", KyivLat + 0.04, KyivLon, dishId);
        var farId = await SeedRestaurantAtAsync("Lviv", 49.8397, 24.0297, dishId);

        var within = await _spatialReader.FindWithinRadiusAsync(origin, radiusKm: 10);

        var ids = within.Select(r => r.Id).ToList();
        ids.Should().Contain(nearId);
        ids.Should().Contain(midId);
        ids.Should().NotContain(farId, "Lviv is ~600 km away, well outside the 10 km radius");

        // Ordering is decided in code by Haversine: nearest first.
        within.Select(r => r.Id).Should().ContainInOrder(nearId, midId);

        // The reported distance is the code-side Haversine, matching SharedKernel.
        var nearPoint = within.Single(r => r.Id == nearId).Location;
        within.Single(r => r.Id == nearId).DistanceKm
            .Should().BeApproximately(Haversine.DistanceKm(origin, nearPoint), 1e-9);
    }

    private async Task<(CategoryId CategoryId, DishId DishId)> SeedCategoryAndDishAsync(string categoryName, string dishName)
    {
        var category = Category.Create(categoryName).Value;
        await _repository.AddCategoryAsync(category);

        var dish = Dish.Create(category.Id, dishName).Value;
        await _repository.AddDishAsync(dish);

        return (category.Id, dish.Id);
    }

    private async Task<RestaurantId> SeedRestaurantAtAsync(string name, double lat, double lon, DishId dishId)
    {
        var address = Address.Create($"{name} street, 1", "City").Value;
        var restaurant = Restaurant.Create(name, address).Value;
        restaurant.SetCoordinates(Coordinates.FromOsmGeoPoint(GeoPoint.Create(lat, lon).Value).Value);
        restaurant.AddMenuItem(dishId, Money.Create(100m, "UAH").Value);

        await _repository.AddRestaurantAsync(restaurant);
        return restaurant.Id;
    }
}
