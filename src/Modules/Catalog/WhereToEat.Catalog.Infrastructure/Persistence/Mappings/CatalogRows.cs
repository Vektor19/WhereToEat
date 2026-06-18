namespace WhereToEat.Catalog.Infrastructure.Persistence.Mappings;

/// <summary>
/// Flat row shapes Dapper materialises straight from the SELECTs in
/// <see cref="Sql.CatalogSql"/>. They are deliberately dumb DTOs (no behaviour): the mappers in
/// <see cref="RowMapper"/> turn them into validated domain objects. Property names match the SQL
/// column aliases so Dapper's default name-based mapping applies with no custom mapper.
/// </summary>
internal sealed class RestaurantRow
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string AddressLine { get; init; } = string.Empty;
    public string? AddressCity { get; init; }

    // Null together when the restaurant has no stored coordinates (not yet geocoded).
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }

    public string? PlaceId { get; init; }
    public string? MapsDeepLink { get; init; }
}

internal sealed class MenuItemRow
{
    public Guid Id { get; init; }
    public Guid DishId { get; init; }
    public decimal PriceAmount { get; init; }
    public string PriceCurrency { get; init; } = string.Empty;
    public string? Weight { get; init; }
    public int Source { get; init; }
    public bool DoNotParse { get; init; }
}

internal sealed class ContactLinkRow
{
    public int Kind { get; init; }
    public string Url { get; init; } = string.Empty;
    public string? Label { get; init; }
}

internal sealed class CategoryRow
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
}

internal sealed class DishRow
{
    public Guid Id { get; init; }
    public Guid CategoryId { get; init; }
    public string CanonicalName { get; init; } = string.Empty;
}
