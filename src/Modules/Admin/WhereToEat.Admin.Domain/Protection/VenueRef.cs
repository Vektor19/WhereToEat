namespace WhereToEat.Admin.Domain.Protection;

/// <summary>
/// An opaque reference to the venue an admin-protection flag is about. An Admin-owned <c>readonly
/// record struct</c> over a <see cref="Guid"/> rather than the Catalog module's <c>RestaurantId</c>,
/// so the Admin domain stays independent of Catalog (the module-isolation fitness rule). It shares the
/// same underlying restaurant Guid in the database.
/// </summary>
public readonly record struct VenueRef(Guid Value)
{
    /// <summary>Wraps a venue Guid (the same value Catalog's RestaurantId carries).</summary>
    public static VenueRef From(Guid value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
