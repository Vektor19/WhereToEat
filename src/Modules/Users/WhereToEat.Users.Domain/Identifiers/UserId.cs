namespace WhereToEat.Users.Domain.Identifiers;

/// <summary>
/// Strongly-typed identifier for a <see cref="Users.User"/> aggregate root. A
/// <c>readonly record struct</c> over a <see cref="Guid"/> — our own internal user id, distinct from
/// the external IdP's subject id (so the IdP stays swappable and the internal id never leaks PII).
/// <see cref="New"/> mints a fresh id; <see cref="From"/> rehydrates a persisted one.
/// </summary>
public readonly record struct UserId(Guid Value)
{
    /// <summary>Mints a new, unique user id.</summary>
    public static UserId New() => new(Guid.NewGuid());

    /// <summary>Rehydrates a user id from a stored <see cref="Guid"/>.</summary>
    public static UserId From(Guid value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
