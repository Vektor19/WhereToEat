using Dapper;
using Microsoft.Data.SqlClient;

namespace WhereToEat.PublicApi.Integration;

/// <summary>
/// Seeds known catalog rows straight into the migrated container via Dapper (raw SQL, no domain
/// types) so the endpoint contracts can be asserted against deterministic data. It writes two
/// restaurants on purpose: one WITH stored coordinates + Place ID + a contact link (for the Map
/// "echoes stored fields" + details "always has contact links" assertions) and one WITHOUT
/// coordinates (for the "no map payload" assertion). Coordinates are written via WKT just like the
/// production data layer — OSM-sourced point, never a Google coordinate (invariant #7).
/// </summary>
internal sealed class SeededCatalog
{
    public Guid CategoryId { get; init; }
    public Guid DishId { get; init; }
    public Guid RestaurantWithCoordsId { get; init; }
    public Guid RestaurantWithoutCoordsId { get; init; }
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public string PlaceId { get; init; } = string.Empty;
    public string MapsDeepLink { get; init; } = string.Empty;
    public string ContactUrl { get; init; } = string.Empty;
}

internal static class CatalogSeeder
{
    public static async Task<SeededCatalog> SeedAsync(string connectionString)
    {
        var seed = new SeededCatalog
        {
            CategoryId = Guid.NewGuid(),
            DishId = Guid.NewGuid(),
            RestaurantWithCoordsId = Guid.NewGuid(),
            RestaurantWithoutCoordsId = Guid.NewGuid(),
            Latitude = 50.4501,
            Longitude = 30.5234,
            PlaceId = "ChIJBUVa4U7P1EAR_kYBF9IxSXY",
            MapsDeepLink = "https://maps.google.com/?cid=12345",
            ContactUrl = "https://borsch.example.com",
        };

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        await connection.ExecuteAsync(
            "INSERT INTO catalog.Category (Id, Name) VALUES (@Id, @Name);",
            new { Id = seed.CategoryId, Name = "Перші страви" }).ConfigureAwait(false);

        await connection.ExecuteAsync(
            "INSERT INTO catalog.Dish (Id, CategoryId, CanonicalName) VALUES (@Id, @CategoryId, @Name);",
            new { Id = seed.DishId, CategoryId = seed.CategoryId, Name = "Борщ" }).ConfigureAwait(false);

        // Restaurant WITH coordinates (WKT is longitude-first, like the production writer).
        var wkt = $"POINT({seed.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)} " +
                  $"{seed.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)})";
        await connection.ExecuteAsync(
            """
            INSERT INTO catalog.Restaurant (Id, Name, AddressLine, AddressCity, Location, PlaceId, MapsDeepLink)
            VALUES (@Id, @Name, @Line, @City, geography::STGeomFromText(@Wkt, 4326), @PlaceId, @DeepLink);
            """,
            new
            {
                Id = seed.RestaurantWithCoordsId,
                Name = "Борщ Хата",
                Line = "вул. Хрещатик, 1",
                City = "Київ",
                Wkt = wkt,
                seed.PlaceId,
                DeepLink = seed.MapsDeepLink,
            }).ConfigureAwait(false);

        await connection.ExecuteAsync(
            "INSERT INTO catalog.ContactLink (RestaurantId, Kind, Url, Label) VALUES (@RestaurantId, @Kind, @Url, @Label);",
            new { RestaurantId = seed.RestaurantWithCoordsId, Kind = 0, Url = seed.ContactUrl, Label = "Сайт" }).ConfigureAwait(false);

        await connection.ExecuteAsync(
            """
            INSERT INTO catalog.MenuItem (Id, RestaurantId, DishId, PriceAmount, PriceCurrency, Weight, Source, DoNotParse)
            VALUES (@Id, @RestaurantId, @DishId, @Amount, @Currency, @Weight, @Source, 0);
            """,
            new
            {
                Id = Guid.NewGuid(),
                RestaurantId = seed.RestaurantWithCoordsId,
                seed.DishId,
                Amount = 120.00m,
                Currency = "UAH",
                Weight = "350 г",
                Source = 0,
            }).ConfigureAwait(false);

        // Restaurant WITHOUT coordinates (Location stays NULL — not yet geocoded).
        await connection.ExecuteAsync(
            "INSERT INTO catalog.Restaurant (Id, Name, AddressLine, AddressCity) VALUES (@Id, @Name, @Line, @City);",
            new
            {
                Id = seed.RestaurantWithoutCoordsId,
                Name = "Кафе Без Координат",
                Line = "вул. Невідома, 5",
                City = "Львів",
            }).ConfigureAwait(false);

        return seed;
    }
}
