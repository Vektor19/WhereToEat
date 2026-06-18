using System.Globalization;
using WhereToEat.Catalog.Domain.Identifiers;
using WhereToEat.Catalog.Domain.Restaurants;
using WhereToEat.Catalog.Domain.Taxonomy;
using WhereToEat.SharedKernel.ValueObjects;

namespace WhereToEat.Catalog.Infrastructure.Persistence.Mappings;

/// <summary>
/// Rehydrates domain objects from the flat <see cref="CatalogRows"/> DTOs. The mappers go through
/// the same domain factories the application uses (<c>Create</c>/<c>AddMenuItem</c>/…), so a row
/// that somehow violated an invariant would surface as a mapping failure rather than a silently
/// invalid aggregate. Because the rows came from data our own writers produced, a factory failure
/// here is a genuine data-corruption bug — hence we throw rather than return a Result.
/// </summary>
internal static class RowMapper
{
    /// <summary>Maps a category row to a <see cref="Category"/>.</summary>
    public static Category ToCategory(CategoryRow row)
    {
        var result = Category.Create(CategoryId.From(row.Id), row.Name);
        return result.IsSuccess
            ? result.Value
            : throw InvalidRow("Category", row.Id, result.Error.Code);
    }

    /// <summary>Maps a dish row to a <see cref="Dish"/>.</summary>
    public static Dish ToDish(DishRow row)
    {
        var result = Dish.Create(DishId.From(row.Id), CategoryId.From(row.CategoryId), row.CanonicalName);
        return result.IsSuccess
            ? result.Value
            : throw InvalidRow("Dish", row.Id, result.Error.Code);
    }

    /// <summary>
    /// Reconstructs a whole <see cref="Restaurant"/> aggregate from its row plus its menu-item and
    /// contact-link rows. Coordinates are rebuilt only when both lat/lng are present (the columns
    /// are null together when the restaurant is not yet geocoded). The OSM-only
    /// <see cref="Coordinates.FromOsmGeoPoint"/> factory is the sole spatial path — there is no
    /// "from Google" mapping, so invariant #7 holds on read too.
    /// </summary>
    public static Restaurant ToRestaurant(
        RestaurantRow row,
        IEnumerable<MenuItemRow> menuItemRows,
        IEnumerable<ContactLinkRow> contactLinkRows)
    {
        var addressResult = Address.Create(row.AddressLine, row.AddressCity);
        if (addressResult.IsFailure)
        {
            throw InvalidRow("Restaurant.Address", row.Id, addressResult.Error.Code);
        }

        var restaurantResult = Restaurant.Create(RestaurantId.From(row.Id), row.Name, addressResult.Value);
        if (restaurantResult.IsFailure)
        {
            throw InvalidRow("Restaurant", row.Id, restaurantResult.Error.Code);
        }

        var restaurant = restaurantResult.Value;

        if (row.Latitude.HasValue && row.Longitude.HasValue)
        {
            var pointResult = GeoPoint.Create(row.Latitude.Value, row.Longitude.Value);
            if (pointResult.IsFailure)
            {
                throw InvalidRow("Restaurant.Coordinates", row.Id, pointResult.Error.Code);
            }

            var coordinatesResult = Coordinates.FromOsmGeoPoint(pointResult.Value, row.PlaceId, row.MapsDeepLink);
            if (coordinatesResult.IsFailure)
            {
                throw InvalidRow("Restaurant.Coordinates", row.Id, coordinatesResult.Error.Code);
            }

            restaurant.SetCoordinates(coordinatesResult.Value);
        }

        foreach (var itemRow in menuItemRows)
        {
            var priceResult = Money.Create(itemRow.PriceAmount, itemRow.PriceCurrency);
            if (priceResult.IsFailure)
            {
                throw InvalidRow("MenuItem.Price", itemRow.Id, priceResult.Error.Code);
            }

            var itemResult = restaurant.AddMenuItem(
                MenuItemId.From(itemRow.Id),
                DishId.From(itemRow.DishId),
                priceResult.Value,
                itemRow.Weight,
                (SourceKind)itemRow.Source,
                itemRow.DoNotParse);

            if (itemResult.IsFailure)
            {
                throw InvalidRow("MenuItem", itemRow.Id, itemResult.Error.Code);
            }
        }

        foreach (var linkRow in contactLinkRows)
        {
            var linkResult = ContactLink.Create((ContactLinkKind)linkRow.Kind, linkRow.Url, linkRow.Label);
            if (linkResult.IsFailure)
            {
                throw InvalidRow("ContactLink", row.Id, linkResult.Error.Code);
            }

            restaurant.AddContactLink(linkResult.Value);
        }

        // Rehydration is not a domain change; clear the events the factories/adders raised so the
        // caller never re-publishes "AddressChanged"/"MenuItemUpserted" for data that already exists.
        restaurant.ClearDomainEvents();

        return restaurant;
    }

    /// <summary>
    /// Builds the WKT "POINT(lon lat)" for a restaurant's coordinates, or <c>null</c> when it has
    /// none. WKT is longitude-first; the invariant-culture format keeps the decimal point a dot
    /// regardless of the runtime locale.
    /// </summary>
    public static string? ToPointWkt(Coordinates? coordinates)
    {
        if (coordinates is null)
        {
            return null;
        }

        var lon = coordinates.Point.Longitude.ToString(CultureInfo.InvariantCulture);
        var lat = coordinates.Point.Latitude.ToString(CultureInfo.InvariantCulture);
        return $"POINT({lon} {lat})";
    }

    private static InvalidOperationException InvalidRow(string what, Guid id, string errorCode)
        => new($"Failed to rehydrate {what} (id {id}) from the database: {errorCode}. This indicates corrupted data.");
}
