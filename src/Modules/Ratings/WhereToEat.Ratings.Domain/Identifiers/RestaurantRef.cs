namespace WhereToEat.Ratings.Domain.Identifiers;

/// <summary>
/// An opaque reference to the restaurant a rating/aggregate belongs to. Deliberately a Ratings-owned
/// <c>readonly record struct</c> over a <see cref="Guid"/> rather than the Catalog module's
/// <c>RestaurantId</c>: the Ratings module must not reference Catalog's domain types (the
/// module-isolation fitness rule). The two share the same underlying restaurant Guid in the database,
/// so a value here round-trips to the same restaurant a Catalog id points at — without a code-level
/// coupling between the modules.
/// </summary>
public readonly record struct RestaurantRef(Guid Value)
{
    /// <summary>Wraps a restaurant Guid (the same value Catalog's RestaurantId carries).</summary>
    public static RestaurantRef From(Guid value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
