namespace WhereToEat.Ratings.Domain.Identifiers;

/// <summary>
/// An opaque reference to the user who left a rating. A Ratings-owned <c>readonly record struct</c>
/// over a <see cref="Guid"/> (not the Users module's identifier), so Ratings stays independent of
/// the Users module per the module-isolation fitness rule. Only the user's internal Guid is stored;
/// no PII is carried here (invariant #11).
/// </summary>
public readonly record struct UserRef(Guid Value)
{
    /// <summary>Wraps a user Guid.</summary>
    public static UserRef From(Guid value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
