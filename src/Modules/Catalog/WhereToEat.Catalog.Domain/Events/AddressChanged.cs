using WhereToEat.Catalog.Domain.Identifiers;
using WhereToEat.SharedKernel.Primitives;

namespace WhereToEat.Catalog.Domain.Events;

/// <summary>
/// Raised by a <see cref="Restaurants.Restaurant"/> when its <see cref="Restaurants.Address"/>
/// changes. The Geo module (Step 8) consumes this to re-geocode the restaurant — the address is
/// the geocoder's input, so a change must trigger fresh OSM/Nominatim coordinates (invariant #7).
/// Carries only the restaurant id and the new address; subscribers load the aggregate themselves.
/// </summary>
public sealed record AddressChanged(
    RestaurantId RestaurantId,
    Restaurants.Address NewAddress,
    DateTimeOffset OccurredOnUtc) : IDomainEvent
{
    /// <summary>Creates the event stamped at <see cref="DateTimeOffset.UtcNow"/>.</summary>
    public static AddressChanged Now(RestaurantId restaurantId, Restaurants.Address newAddress)
        => new(restaurantId, newAddress, DateTimeOffset.UtcNow);
}
