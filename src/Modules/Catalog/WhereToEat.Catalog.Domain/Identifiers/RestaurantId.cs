namespace WhereToEat.Catalog.Domain.Identifiers;

/// <summary>
/// Strongly-typed identifier for a <see cref="Restaurants.Restaurant"/> aggregate root. A
/// <c>readonly record struct</c> over a <see cref="Guid"/>. <see cref="New"/> mints a fresh id;
/// <see cref="From"/> rehydrates a persisted one.
/// </summary>
public readonly record struct RestaurantId(Guid Value)
{
    /// <summary>Mints a new, unique restaurant id.</summary>
    public static RestaurantId New() => new(Guid.NewGuid());

    /// <summary>Rehydrates a restaurant id from a stored <see cref="Guid"/>.</summary>
    public static RestaurantId From(Guid value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
