using System.Globalization;
using Dapper;
using Microsoft.Data.SqlClient;

namespace WhereToEat.AdminApi.Integration;

/// <summary>
/// Seeds the script-created catalog tables directly with Dapper so the admin CRUD tests have rows to
/// edit, and reads them back for assertions. It mirrors the SQL schema (Steps 4–5) exactly — the admin
/// host's EF maps onto the same tables database-first, so seeding via Dapper and editing via EF prove
/// they share one schema-of-record.
/// </summary>
internal sealed class CatalogSeeder
{
    private readonly string _connectionString;

    public CatalogSeeder(string connectionString) => _connectionString = connectionString;

    /// <summary>Inserts a category and returns its id.</summary>
    public async Task<Guid> AddCategoryAsync(string name)
    {
        var id = Guid.NewGuid();
        await using var conn = new SqlConnection(_connectionString);
        await conn.ExecuteAsync(
            "INSERT INTO catalog.Category (Id, Name) VALUES (@Id, @Name);",
            new { Id = id, Name = name });
        return id;
    }

    /// <summary>Inserts a dish in a category and returns its id.</summary>
    public async Task<Guid> AddDishAsync(Guid categoryId, string canonicalName)
    {
        var id = Guid.NewGuid();
        await using var conn = new SqlConnection(_connectionString);
        await conn.ExecuteAsync(
            "INSERT INTO catalog.Dish (Id, CategoryId, CanonicalName) VALUES (@Id, @CategoryId, @CanonicalName);",
            new { Id = id, CategoryId = categoryId, CanonicalName = canonicalName });
        return id;
    }

    /// <summary>Inserts a restaurant and returns its id.</summary>
    public async Task<Guid> AddRestaurantAsync(string name, string addressLine, string? city = null)
    {
        var id = Guid.NewGuid();
        await using var conn = new SqlConnection(_connectionString);
        await conn.ExecuteAsync(
            "INSERT INTO catalog.Restaurant (Id, Name, AddressLine, AddressCity) " +
            "VALUES (@Id, @Name, @AddressLine, @AddressCity);",
            new { Id = id, Name = name, AddressLine = addressLine, AddressCity = city });
        return id;
    }

    /// <summary>Inserts a menu item (provenance + DoNotParse) and returns its id.</summary>
    public async Task<Guid> AddMenuItemAsync(
        Guid restaurantId,
        Guid dishId,
        decimal priceAmount,
        string currency,
        string? weight = null,
        int source = 0,
        bool doNotParse = false)
    {
        var id = Guid.NewGuid();
        await using var conn = new SqlConnection(_connectionString);
        await conn.ExecuteAsync(
            "INSERT INTO catalog.MenuItem (Id, RestaurantId, DishId, PriceAmount, PriceCurrency, Weight, Source, DoNotParse) " +
            "VALUES (@Id, @RestaurantId, @DishId, @PriceAmount, @PriceCurrency, @Weight, @Source, @DoNotParse);",
            new
            {
                Id = id,
                RestaurantId = restaurantId,
                DishId = dishId,
                PriceAmount = priceAmount,
                PriceCurrency = currency,
                Weight = weight,
                Source = source,
                DoNotParse = doNotParse,
            });
        return id;
    }

    /// <summary>Inserts a photo (generic or real) and returns its id.</summary>
    public async Task<Guid> AddPhotoAsync(Guid dishId, bool isGeneric, bool permissionGranted, string url)
    {
        var id = Guid.NewGuid();
        await using var conn = new SqlConnection(_connectionString);
        await conn.ExecuteAsync(
            "INSERT INTO catalog.Photo (Id, DishId, IsGeneric, PermissionGranted, Url) " +
            "VALUES (@Id, @DishId, @IsGeneric, @PermissionGranted, @Url);",
            new { Id = id, DishId = dishId, IsGeneric = isGeneric, PermissionGranted = permissionGranted, Url = url });
        return id;
    }

    /// <summary>Reads a menu item's editable fields back for assertions.</summary>
    public async Task<MenuItemRow?> GetMenuItemAsync(Guid menuItemId)
    {
        await using var conn = new SqlConnection(_connectionString);
        return await conn.QuerySingleOrDefaultAsync<MenuItemRow>(
            "SELECT Id, RestaurantId, DishId, PriceAmount, PriceCurrency, Weight, Source, DoNotParse " +
            "FROM catalog.MenuItem WHERE Id = @Id;",
            new { Id = menuItemId });
    }

    /// <summary>Reads a restaurant's address back for assertions.</summary>
    public async Task<(string AddressLine, string? City)> GetAddressAsync(Guid restaurantId)
    {
        await using var conn = new SqlConnection(_connectionString);
        var row = await conn.QuerySingleAsync<(string AddressLine, string? AddressCity)>(
            "SELECT AddressLine, AddressCity FROM catalog.Restaurant WHERE Id = @Id;",
            new { Id = restaurantId });
        return (row.AddressLine, row.AddressCity);
    }

    /// <summary>Reads a photo's permission flag back for assertions.</summary>
    public async Task<bool> GetPhotoPermissionAsync(Guid photoId)
    {
        await using var conn = new SqlConnection(_connectionString);
        return await conn.QuerySingleAsync<bool>(
            "SELECT PermissionGranted FROM catalog.Photo WHERE Id = @Id;",
            new { Id = photoId });
    }

    /// <summary>Reads the restaurant-level DoNotUpdate flag (null when no protection row exists).</summary>
    public async Task<bool?> GetDoNotUpdateAsync(Guid restaurantId)
    {
        await using var conn = new SqlConnection(_connectionString);
        return await conn.QuerySingleOrDefaultAsync<bool?>(
            "SELECT DoNotUpdate FROM admin.RestaurantProtection WHERE RestaurantId = @Id;",
            new { Id = restaurantId });
    }

    /// <summary>Counts the menu items for a restaurant (to prove a DoNotUpdate parse wrote nothing).</summary>
    public async Task<int> CountMenuItemsAsync(Guid restaurantId)
    {
        await using var conn = new SqlConnection(_connectionString);
        return await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM catalog.MenuItem WHERE RestaurantId = @Id;",
            new { Id = restaurantId });
    }

    /// <summary>Reads a menu item's price for a given restaurant + dish (to prove DoNotParse untouched).</summary>
    public async Task<decimal?> GetPriceAsync(Guid restaurantId, Guid dishId)
    {
        await using var conn = new SqlConnection(_connectionString);
        return await conn.QuerySingleOrDefaultAsync<decimal?>(
            "SELECT PriceAmount FROM catalog.MenuItem WHERE RestaurantId = @R AND DishId = @D;",
            new { R = restaurantId, D = dishId });
    }

    /// <summary>The editable fields of a menu item row.</summary>
    public sealed record MenuItemRow(
        Guid Id,
        Guid RestaurantId,
        Guid DishId,
        decimal PriceAmount,
        string PriceCurrency,
        string? Weight,
        int Source,
        bool DoNotParse)
    {
        public string PriceString => PriceAmount.ToString(CultureInfo.InvariantCulture);
    }
}
