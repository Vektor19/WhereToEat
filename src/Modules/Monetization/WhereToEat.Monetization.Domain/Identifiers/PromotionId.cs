namespace WhereToEat.Monetization.Domain.Identifiers;

/// <summary>
/// Strongly-typed identifier for a <see cref="Monetization.Promotion"/>. A <c>readonly record
/// struct</c> over a <see cref="Guid"/>. <see cref="New"/> mints a fresh id; <see cref="From"/>
/// rehydrates a persisted one.
/// </summary>
public readonly record struct PromotionId(Guid Value)
{
    /// <summary>Mints a new, unique promotion id.</summary>
    public static PromotionId New() => new(Guid.NewGuid());

    /// <summary>Rehydrates a promotion id from a stored <see cref="Guid"/>.</summary>
    public static PromotionId From(Guid value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
