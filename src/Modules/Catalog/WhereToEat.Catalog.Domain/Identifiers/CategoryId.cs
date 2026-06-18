namespace WhereToEat.Catalog.Domain.Identifiers;

/// <summary>
/// Strongly-typed identifier for a <see cref="Taxonomy.Category"/>. A <c>readonly record
/// struct</c> over a <see cref="Guid"/> so the type system prevents passing a <c>DishId</c>
/// (or a bare <see cref="Guid"/>) where a category id is required, while equality and hashing
/// come for free by value. <see cref="New"/> mints a fresh id; <see cref="From"/> rehydrates a
/// persisted one.
/// </summary>
public readonly record struct CategoryId(Guid Value)
{
    /// <summary>Mints a new, unique category id.</summary>
    public static CategoryId New() => new(Guid.NewGuid());

    /// <summary>Rehydrates a category id from a stored <see cref="Guid"/>.</summary>
    public static CategoryId From(Guid value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
