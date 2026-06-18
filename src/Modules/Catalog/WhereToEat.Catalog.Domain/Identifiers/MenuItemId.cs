namespace WhereToEat.Catalog.Domain.Identifiers;

/// <summary>
/// Strongly-typed identifier for a <see cref="Restaurants.MenuItem"/> (the dish-at-restaurant
/// join). A <c>readonly record struct</c> over a <see cref="Guid"/>. <see cref="New"/> mints a
/// fresh id; <see cref="From"/> rehydrates a persisted one.
/// </summary>
public readonly record struct MenuItemId(Guid Value)
{
    /// <summary>Mints a new, unique menu-item id.</summary>
    public static MenuItemId New() => new(Guid.NewGuid());

    /// <summary>Rehydrates a menu-item id from a stored <see cref="Guid"/>.</summary>
    public static MenuItemId From(Guid value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
