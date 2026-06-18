namespace WhereToEat.Monetization.Domain.Identifiers;

/// <summary>
/// An opaque reference to the venue a Verified status / promotion / ad placement is about. A
/// Monetization-owned <c>readonly record struct</c> over a <see cref="Guid"/> rather than the Catalog
/// module's <c>RestaurantId</c>, so Monetization stays independent of Catalog (the module-isolation
/// fitness rule). It shares the same underlying restaurant Guid in the database.
/// </summary>
public readonly record struct VenueRef(Guid Value)
{
    /// <summary>Wraps a venue Guid (the same value Catalog's RestaurantId carries).</summary>
    public static VenueRef From(Guid value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
