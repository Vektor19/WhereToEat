namespace WhereToEat.Ratings.Domain.Identifiers;

/// <summary>
/// Strongly-typed identifier for a single user's <see cref="Ratings.Rating"/>. A
/// <c>readonly record struct</c> over a <see cref="Guid"/>. <see cref="New"/> mints a fresh id;
/// <see cref="From"/> rehydrates a persisted one.
/// </summary>
public readonly record struct RatingId(Guid Value)
{
    /// <summary>Mints a new, unique rating id.</summary>
    public static RatingId New() => new(Guid.NewGuid());

    /// <summary>Rehydrates a rating id from a stored <see cref="Guid"/>.</summary>
    public static RatingId From(Guid value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
