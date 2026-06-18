using System.Globalization;
using Dapper;
using Microsoft.Data.SqlClient;

namespace WhereToEat.PublicApi.Integration;

/// <summary>
/// Seeds a deterministic recommendation scenario straight into the migrated container via Dapper:
/// one dish offered by two restaurants at different prices and ratings, the materialized rating
/// aggregate for each, and a city-wide price-median row for the dish. It seeds <b>both</b> the
/// rating aggregate and the median table <b>directly</b> so the recommendation test does NOT depend
/// on the Step 12 nightly job or the rating-recompute job — the data is present before the call
/// (median-data + rating independence, per the Step 7 testing requirement).
/// </summary>
internal sealed class SeededRecommendation
{
    public Guid CategoryId { get; init; }
    public Guid DishId { get; init; }

    /// <summary>Cheaper basket, lower rating — should win an explicit price sort.</summary>
    public Guid CheapLowRatedId { get; init; }

    /// <summary>Pricier basket, higher rating — should win a quality-weighted composite sort.</summary>
    public Guid PriceyHighRatedId { get; init; }

    public decimal CheapPrice { get; init; }
    public decimal PriceyPrice { get; init; }
    public decimal CityWideMedian { get; init; }
}

internal static class RecommendSeeder
{
    public static async Task<SeededRecommendation> SeedAsync(string connectionString)
    {
        var seed = new SeededRecommendation
        {
            CategoryId = Guid.NewGuid(),
            DishId = Guid.NewGuid(),
            CheapLowRatedId = Guid.NewGuid(),
            PriceyHighRatedId = Guid.NewGuid(),
            CheapPrice = 80m,
            PriceyPrice = 160m,
            CityWideMedian = 120m,
        };

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        await connection.ExecuteAsync(
            "INSERT INTO catalog.Category (Id, Name) VALUES (@Id, @Name);",
            new { Id = seed.CategoryId, Name = "Перші страви" }).ConfigureAwait(false);

        await connection.ExecuteAsync(
            "INSERT INTO catalog.Dish (Id, CategoryId, CanonicalName) VALUES (@Id, @CategoryId, @Name);",
            new { Id = seed.DishId, CategoryId = seed.CategoryId, Name = "Борщ" }).ConfigureAwait(false);

        // Two restaurants near Kyiv centre (so they survive the radius pre-filter), each offering the
        // dish — one cheap+low-rated, one pricey+high-rated.
        await InsertRestaurantAsync(connection, seed.CheapLowRatedId, "Дешева Борщівня", 50.4501, 30.5234);
        await InsertRestaurantAsync(connection, seed.PriceyHighRatedId, "Преміум Борщ", 50.4505, 30.5240);

        await InsertMenuItemAsync(connection, seed.CheapLowRatedId, seed.DishId, seed.CheapPrice);
        await InsertMenuItemAsync(connection, seed.PriceyHighRatedId, seed.DishId, seed.PriceyPrice);

        // Materialized rating aggregates (seeded directly — no rating-recompute job needed).
        // Cheap venue: 200 ratings summing to 600 => raw avg 3.0. Pricey venue: 200 => 960 => 4.8.
        await UpsertRatingAggregateAsync(connection, seed.CheapLowRatedId, scoreSum: 600d, scoreCount: 200);
        await UpsertRatingAggregateAsync(connection, seed.PriceyHighRatedId, scoreSum: 960d, scoreCount: 200);

        // City-wide price median for the dish (seeded directly — no nightly median job needed).
        await connection.ExecuteAsync(
            """
            INSERT INTO recommendation.PriceMedian (AreaKey, CategoryId, DishId, MedianAmount, Currency, SampleSize)
            VALUES ('*', NULL, @DishId, @Median, 'UAH', 50);
            """,
            new { seed.DishId, Median = seed.CityWideMedian }).ConfigureAwait(false);

        return seed;
    }

    private static async Task InsertRestaurantAsync(SqlConnection connection, Guid id, string name, double lat, double lng)
    {
        var wkt = $"POINT({lng.ToString(CultureInfo.InvariantCulture)} {lat.ToString(CultureInfo.InvariantCulture)})";
        await connection.ExecuteAsync(
            """
            INSERT INTO catalog.Restaurant (Id, Name, AddressLine, AddressCity, Location)
            VALUES (@Id, @Name, @Line, @City, geography::STGeomFromText(@Wkt, 4326));
            """,
            new { Id = id, Name = name, Line = "вул. Хрещатик, 1", City = "Київ", Wkt = wkt }).ConfigureAwait(false);
    }

    private static async Task InsertMenuItemAsync(SqlConnection connection, Guid restaurantId, Guid dishId, decimal price)
        => await connection.ExecuteAsync(
            """
            INSERT INTO catalog.MenuItem (Id, RestaurantId, DishId, PriceAmount, PriceCurrency, Weight, Source, DoNotParse)
            VALUES (@Id, @RestaurantId, @DishId, @Amount, 'UAH', N'350 г', 0, 0);
            """,
            new { Id = Guid.NewGuid(), RestaurantId = restaurantId, DishId = dishId, Amount = price }).ConfigureAwait(false);

    private static async Task UpsertRatingAggregateAsync(SqlConnection connection, Guid restaurantId, double scoreSum, long scoreCount)
        => await connection.ExecuteAsync(
            """
            INSERT INTO ratings.RatingAggregate (RestaurantId, ScoreSum, ScoreCount)
            VALUES (@RestaurantId, @ScoreSum, @ScoreCount);
            """,
            new { RestaurantId = restaurantId, ScoreSum = scoreSum, ScoreCount = scoreCount }).ConfigureAwait(false);
}
