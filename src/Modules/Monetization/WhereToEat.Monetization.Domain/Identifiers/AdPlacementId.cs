namespace WhereToEat.Monetization.Domain.Identifiers;

/// <summary>
/// Strongly-typed identifier for an <see cref="Monetization.AdPlacement"/>. A <c>readonly record
/// struct</c> over a <see cref="Guid"/>. <see cref="New"/> mints a fresh id; <see cref="From"/>
/// rehydrates a persisted one.
/// </summary>
public readonly record struct AdPlacementId(Guid Value)
{
    /// <summary>Mints a new, unique ad-placement id.</summary>
    public static AdPlacementId New() => new(Guid.NewGuid());

    /// <summary>Rehydrates an ad-placement id from a stored <see cref="Guid"/>.</summary>
    public static AdPlacementId From(Guid value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
