namespace WhereToEat.Contracts.IntegrationEvents;

/// <summary>
/// Published when an already-anonymized analytics event has been ingested, so analytics
/// ingestion can later move to its own service without a rewrite (design §"Messaging").
/// Carries only the anonymized shape — never raw PII or precise lat/lng (invariant #11):
/// the kind, the coarse area geohash, the hour-truncated time, and the hashed actor id.
/// <para>
/// <see cref="CoarseGeohash"/> and <see cref="HashedActorId"/> are nullable to match the domain:
/// most events are location-less (no opt-in geo) and/or actor-less, so the domain
/// <c>AnalyticsEvent.CoarseLocation</c> and <c>Actor</c> are both optional.
/// </para>
/// </summary>
public sealed record AnalyticsEventRaised(
    Guid EventId,
    DateTimeOffset OccurredOnUtc,
    string EventKind,
    string? CoarseGeohash,
    DateTimeOffset HourBucketUtc,
    string? HashedActorId) : IIntegrationEvent;
