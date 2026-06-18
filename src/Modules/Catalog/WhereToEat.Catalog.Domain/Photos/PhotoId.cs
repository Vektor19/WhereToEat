namespace WhereToEat.Catalog.Domain.Photos;

/// <summary>
/// Strongly-typed identifier for a <see cref="Photo"/>. A <c>readonly record struct</c> over a
/// <see cref="Guid"/>. <see cref="New"/> mints a fresh id; <see cref="From"/> rehydrates a persisted one.
/// </summary>
public readonly record struct PhotoId(Guid Value)
{
    /// <summary>Mints a new, unique photo id.</summary>
    public static PhotoId New() => new(Guid.NewGuid());

    /// <summary>Rehydrates a photo id from a stored <see cref="Guid"/>.</summary>
    public static PhotoId From(Guid value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
