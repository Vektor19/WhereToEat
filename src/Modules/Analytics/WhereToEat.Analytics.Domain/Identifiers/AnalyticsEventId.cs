namespace WhereToEat.Analytics.Domain.Identifiers;

/// <summary>
/// Strongly-typed identifier for an <see cref="Analytics.AnalyticsEvent"/>. A <c>readonly record
/// struct</c> over a <see cref="Guid"/>. <see cref="New"/> mints a fresh id; <see cref="From"/>
/// rehydrates a persisted one. This is the row's own id — it is <b>not</b> derived from any user/
/// session id (those are hashed away).
/// </summary>
public readonly record struct AnalyticsEventId(Guid Value)
{
    /// <summary>Mints a new, unique event id.</summary>
    public static AnalyticsEventId New() => new(Guid.NewGuid());

    /// <summary>Rehydrates an event id from a stored <see cref="Guid"/>.</summary>
    public static AnalyticsEventId From(Guid value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
