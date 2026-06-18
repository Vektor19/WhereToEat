using WhereToEat.Analytics.Domain;

namespace WhereToEat.Analytics.Application;

/// <summary>
/// The <b>raw</b>, pre-anonymization analytics payload as it arrives at the ingest endpoint — the
/// only place a raw user/session id or a precise coordinate is ever held, in memory, transiently. The
/// <see cref="IAnonymizer"/> turns this into the persistable anonymized <see cref="AnalyticsEvent"/>
/// <b>before</b> anything is written, so the raw fields below never reach the database (invariant #11):
/// <list type="bullet">
///   <item><b>retained</b> as-is: <see cref="CategoryIds"/>, <see cref="DishIds"/>,
///   <see cref="SortMode"/>, <see cref="Filters"/>, <see cref="Position"/>;</item>
///   <item><b>coarsened</b>: <see cref="OccurredAtUtc"/> → hour bucket, the precise
///   <see cref="Latitude"/>/<see cref="Longitude"/> → a neighbourhood-grade geohash;</item>
///   <item><b>hashed</b>: <see cref="UserId"/> / <see cref="SessionId"/> → a rotating-salted digest;</item>
///   <item><b>dropped</b>: the precise lat/lng and the raw id never survive anonymization.</item>
/// </list>
/// </summary>
public sealed record RawAnalyticsEvent
{
    /// <summary>The kind of event being reported.</summary>
    public EventKind Kind { get; init; }

    /// <summary>
    /// The precise event time (any precision). Coarsened to an hour bucket at ingest; minute/second
    /// precision is dropped before persistence.
    /// </summary>
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// The restaurant the event concerns (a venue id — retained, it identifies a place, not a person).
    /// This is what the per-restaurant rollup groups by.
    /// </summary>
    public Guid? RestaurantId { get; init; }

    /// <summary>The category ids involved (retained verbatim — non-identifying demand signal).</summary>
    public IReadOnlyList<Guid>? CategoryIds { get; init; }

    /// <summary>The dish ids involved (retained verbatim — non-identifying demand signal).</summary>
    public IReadOnlyList<Guid>? DishIds { get; init; }

    /// <summary>The sort mode key (retained verbatim), or null when not applicable.</summary>
    public string? SortMode { get; init; }

    /// <summary>The applied filter keys (retained verbatim), in selection order.</summary>
    public IReadOnlyList<string>? Filters { get; init; }

    /// <summary>The 1-based result-list position (retained verbatim), or null.</summary>
    public int? Position { get; init; }

    /// <summary>
    /// The raw user id, if any — <b>dropped</b> after being mixed with the rotating salt and hashed.
    /// Never persisted.
    /// </summary>
    public string? UserId { get; init; }

    /// <summary>
    /// The raw session id, if any — used (with <see cref="UserId"/>) to derive the actor hash, then
    /// <b>dropped</b>. Never persisted.
    /// </summary>
    public string? SessionId { get; init; }

    /// <summary>
    /// The precise latitude, if the user opted into location — <b>dropped</b> after a coarse geohash is
    /// derived. Never persisted.
    /// </summary>
    public double? Latitude { get; init; }

    /// <summary>
    /// The precise longitude, if the user opted into location — <b>dropped</b> after a coarse geohash is
    /// derived. Never persisted.
    /// </summary>
    public double? Longitude { get; init; }
}
