namespace WhereToEat.Catalog.Domain.Identifiers;

/// <summary>
/// Strongly-typed identifier for a <see cref="Taxonomy.Dish"/>. A <c>readonly record struct</c>
/// over a <see cref="Guid"/> so a dish id can never be confused with a category/restaurant id at
/// a call site. <see cref="New"/> mints a fresh id; <see cref="From"/> rehydrates a persisted one.
/// </summary>
public readonly record struct DishId(Guid Value)
{
    /// <summary>Mints a new, unique dish id.</summary>
    public static DishId New() => new(Guid.NewGuid());

    /// <summary>Rehydrates a dish id from a stored <see cref="Guid"/>.</summary>
    public static DishId From(Guid value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
