using WhereToEat.Analytics.Domain.Anonymization;
using WhereToEat.Analytics.Domain.Identifiers;
using WhereToEat.SharedKernel.Primitives;
using WhereToEat.SharedKernel.Results;

namespace WhereToEat.Analytics.Domain;

/// <summary>
/// The <b>anonymized</b> product-analytics event — the persistable shape after the retain/hash/drop
/// rules have been applied (§8.1 / invariant #11). By construction it can only ever hold the
/// privacy-safe pieces:
/// <list type="bullet">
///   <item><b>retained:</b> the <see cref="EventDimensions"/> (dish/category ids, sort mode, filters,
///   result position);</item>
///   <item><b>hashed:</b> the optional <see cref="HashedActorId"/> (rotating-salted — never the raw
///   user/session id);</item>
///   <item><b>coarsened:</b> the optional <see cref="Geohash"/> (neighbourhood-grade — never precise
///   lat/lng) and the <see cref="HourBucket"/> occurrence time (hour-truncated).</item>
/// </list>
///
/// <para>
/// There is deliberately <b>no</b> property for a raw user id, a precise coordinate, or a minute-grade
/// timestamp — the type cannot represent dropped data, so a row built from it is anonymized by
/// construction. This is the domain shape only; the ingest endpoint, the <c>IAnonymizer</c> that
/// derives these values from a raw payload, and the append-optimized writer are Step 11.
/// </para>
/// </summary>
public sealed class AnalyticsEvent : AggregateRoot<AnalyticsEventId>
{
    private AnalyticsEvent(
        AnalyticsEventId id,
        EventKind kind,
        HourBucket occurredAtHour,
        EventDimensions dimensions,
        HashedActorId? actor,
        Geohash? coarseLocation)
        : base(id)
    {
        Kind = kind;
        OccurredAtHour = occurredAtHour;
        Dimensions = dimensions;
        Actor = actor;
        CoarseLocation = coarseLocation;
    }

    /// <summary>The kind of event.</summary>
    public EventKind Kind { get; }

    /// <summary>The hour-truncated occurrence time (UTC) — minute/second precision is dropped.</summary>
    public HourBucket OccurredAtHour { get; }

    /// <summary>The retained non-identifying dimensions.</summary>
    public EventDimensions Dimensions { get; }

    /// <summary>The rotating-salted hash of the actor, or null for an actor-less event.</summary>
    public HashedActorId? Actor { get; }

    /// <summary>The coarse neighbourhood-grade geohash, or null when no (opt-in) location was given.</summary>
    public Geohash? CoarseLocation { get; }

    /// <summary>Records an anonymized event with a fresh id.</summary>
    public static Result<AnalyticsEvent> Record(
        EventKind kind,
        HourBucket occurredAtHour,
        EventDimensions dimensions,
        HashedActorId? actor = null,
        Geohash? coarseLocation = null)
        => Record(AnalyticsEventId.New(), kind, occurredAtHour, dimensions, actor, coarseLocation);

    /// <summary>
    /// Records an anonymized event with an explicit id (for rehydration/seeding), rejecting a null
    /// hour bucket or dimensions. The optional actor/location are already privacy-safe value objects,
    /// so no raw value can enter here.
    /// </summary>
    public static Result<AnalyticsEvent> Record(
        AnalyticsEventId id,
        EventKind kind,
        HourBucket occurredAtHour,
        EventDimensions dimensions,
        HashedActorId? actor = null,
        Geohash? coarseLocation = null)
    {
        if (occurredAtHour is null)
        {
            return Result.Failure<AnalyticsEvent>(
                Error.Validation("AnalyticsEvent.TimeRequired", "An analytics event must carry an hour-truncated time."));
        }

        if (dimensions is null)
        {
            return Result.Failure<AnalyticsEvent>(
                Error.Validation("AnalyticsEvent.DimensionsRequired", "An analytics event must carry its retained dimensions."));
        }

        return Result.Success(new AnalyticsEvent(id, kind, occurredAtHour, dimensions, actor, coarseLocation));
    }
}
