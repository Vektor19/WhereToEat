using System.Globalization;
using Dapper;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using WhereToEat.BuildingBlocks.Persistence;
using WhereToEat.Contracts.Recommendation;
using WhereToEat.Recommendation.Infrastructure.Medians;
using Xunit;

namespace WhereToEat.Worker.Integration;

/// <summary>
/// The nightly-median proof (Step 12): the <see cref="PriceMedianWriter"/> recomputes per-area /
/// per-category medians (with the city-wide fallback below threshold N) from seeded menu prices and
/// writes them into the Step 7 <c>recommendation.PriceMedian</c> table, and the Step 7
/// <see cref="DbPriceMedianProvider"/> reads back EXACTLY those rows — DB-only, no Redis. This is the
/// hand-off between Step 12 (write) and Step 7 (read) asserted end-to-end on a real DB.
/// </summary>
[Collection("worker-sql")]
public sealed class NightlyPriceMedianTests
{
    // One ~0.05° grid cell ("dense" area) gets enough samples for a per-area row; a second restaurant
    // sits in a different cell with too few samples, so it must fall back to the city-wide median.
    private const double DenseLat = 50.451;
    private const double DenseLng = 30.523;
    private const double SparseLat = 49.841; // a clearly different grid cell
    private const double SparseLng = 24.031;

    private readonly WorkerSqlServerFixture _fixture;

    public NightlyPriceMedianTests(WorkerSqlServerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Writes_per_area_and_city_wide_rows_that_the_db_provider_reads_back()
    {
        await ResetTablesAsync();

        var categoryId = Guid.NewGuid();
        var dishId = Guid.NewGuid();
        await SeedTaxonomyAsync(categoryId, dishId);

        // Dense area: 5 menu items for the dish at 100/110/120/130/140 → per-area median 120 (N=5).
        var densePrices = new[] { 100m, 110m, 120m, 130m, 140m };
        foreach (var price in densePrices)
        {
            await SeedRestaurantWithMenuItemAsync(dishId, price, DenseLat, DenseLng);
        }

        // Sparse area: a single 500 item (below N) → no per-area row; counts only city-wide.
        await SeedRestaurantWithMenuItemAsync(dishId, 500m, SparseLat, SparseLng);

        // City-wide dish median over all six: sorted 100,110,120,130,140,500 → (120+130)/2 = 125.
        const decimal expectedCityWideDishMedian = 125m;
        const decimal expectedDenseDishMedian = 120m;

        var factory = new SqlConnectionFactory(_fixture.ConnectionString);
        var writer = new PriceMedianWriter(factory);
        var provider = new DbPriceMedianProvider(factory);

        // --- Act: the nightly recompute (threshold N = 5) ---------------------------------------
        var rowsWritten = await writer.RecomputeAsync(areaSampleThreshold: 5);
        rowsWritten.Should().BeGreaterThan(0);

        // --- Assert: the provider (Step 7 read path) returns exactly what the job wrote -----------
        var selection = new[] { SelectedItem.Dish(dishId) };

        // A user IN the dense cell reads the per-area median (120), not the city-wide one.
        var denseMedian = await provider.GetMedianBasketAmountAsync(
            selection, new UserGeo(DenseLat, DenseLng));
        denseMedian.Should().Be(expectedDenseDishMedian);

        // A user with no location reads the city-wide fallback (125).
        var cityWideMedian = await provider.GetMedianBasketAmountAsync(selection, userGeo: null);
        cityWideMedian.Should().Be(expectedCityWideDishMedian);

        // A user in the SPARSE cell (below N there → no per-area row) falls back to the city-wide one.
        var sparseMedian = await provider.GetMedianBasketAmountAsync(
            selection, new UserGeo(SparseLat, SparseLng));
        sparseMedian.Should().Be(expectedCityWideDishMedian);

        // And the table itself holds the expected per-area + city-wide dish rows (the exact written shape).
        await using var conn = new SqlConnection(_fixture.ConnectionString);
        var dishRows = (await conn.QueryAsync<(string AreaKey, decimal MedianAmount, int SampleSize)>(
            "SELECT AreaKey, MedianAmount, SampleSize FROM recommendation.PriceMedian WHERE DishId = @DishId;",
            new { DishId = dishId })).ToList();

        dishRows.Should().HaveCount(2, "exactly one city-wide row and one per-area (dense) row — the sparse area is below N");
        dishRows.Should().Contain(r => r.AreaKey == "*" && r.MedianAmount == expectedCityWideDishMedian && r.SampleSize == 6);
        dishRows.Should().Contain(r => r.AreaKey != "*" && r.MedianAmount == expectedDenseDishMedian && r.SampleSize == 5);
        dishRows.Should().NotContain(r => r.AreaKey != "*" && r.SampleSize < 5); // sparse area never got a per-area row
    }

    private async Task ResetTablesAsync()
    {
        await using var conn = new SqlConnection(_fixture.ConnectionString);
        await conn.ExecuteAsync(
            """
            DELETE FROM recommendation.PriceMedian;
            DELETE FROM catalog.MenuItem;
            DELETE FROM catalog.Restaurant;
            DELETE FROM catalog.Dish;
            DELETE FROM catalog.Category;
            """);
    }

    private async Task SeedTaxonomyAsync(Guid categoryId, Guid dishId)
    {
        await using var conn = new SqlConnection(_fixture.ConnectionString);
        await conn.ExecuteAsync(
            "INSERT INTO catalog.Category (Id, Name) VALUES (@Id, @Name);",
            new { Id = categoryId, Name = "Перші страви" });
        await conn.ExecuteAsync(
            "INSERT INTO catalog.Dish (Id, CategoryId, CanonicalName) VALUES (@Id, @CategoryId, @Name);",
            new { Id = dishId, CategoryId = categoryId, Name = "Борщ" });
    }

    private async Task SeedRestaurantWithMenuItemAsync(Guid dishId, decimal price, double lat, double lng)
    {
        var restaurantId = Guid.NewGuid();
        var wkt = string.Format(CultureInfo.InvariantCulture, "POINT({0} {1})", lng, lat); // WKT is lon-first

        await using var conn = new SqlConnection(_fixture.ConnectionString);
        await conn.ExecuteAsync(
            """
            INSERT INTO catalog.Restaurant (Id, Name, AddressLine, AddressCity, Location)
            VALUES (@Id, @Name, @AddressLine, NULL, geography::STGeomFromText(@Wkt, 4326));
            """,
            new { Id = restaurantId, Name = "Test", AddressLine = "вул. Тестова, 1", Wkt = wkt });

        await conn.ExecuteAsync(
            """
            INSERT INTO catalog.MenuItem (Id, RestaurantId, DishId, PriceAmount, PriceCurrency, Source, DoNotParse)
            VALUES (@Id, @RestaurantId, @DishId, @Price, 'UAH', 0, 0);
            """,
            new { Id = Guid.NewGuid(), RestaurantId = restaurantId, DishId = dishId, Price = price });
    }
}
