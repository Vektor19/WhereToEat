using WhereToEat.Catalog.Domain.Identifiers;
using WhereToEat.SharedKernel.Primitives;

namespace WhereToEat.Catalog.Domain.Events;

/// <summary>
/// Raised by a <see cref="Restaurants.Restaurant"/> when a <see cref="Restaurants.MenuItem"/> is
/// added or its price/details are updated. Downstream concerns (search re-index, analytics,
/// price-median refresh in later steps) react to this without the catalog aggregate knowing about
/// them. Carries the restaurant, the affected menu item, and the dish it represents.
/// </summary>
public sealed record MenuItemUpserted(
    RestaurantId RestaurantId,
    MenuItemId MenuItemId,
    DishId DishId,
    DateTimeOffset OccurredOnUtc) : IDomainEvent
{
    /// <summary>Creates the event stamped at <see cref="DateTimeOffset.UtcNow"/>.</summary>
    public static MenuItemUpserted Now(RestaurantId restaurantId, MenuItemId menuItemId, DishId dishId)
        => new(restaurantId, menuItemId, dishId, DateTimeOffset.UtcNow);
}
