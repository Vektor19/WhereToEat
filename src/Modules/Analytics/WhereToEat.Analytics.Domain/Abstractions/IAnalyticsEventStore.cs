namespace WhereToEat.Analytics.Domain.Abstractions;

/// <summary>
/// The Analytics module's <b>append-optimized</b> persistence port: it only ever <i>appends</i>
/// anonymized events (events are immutable facts; there is no update/delete on the port). The Dapper
/// adapter in <c>WhereToEat.Analytics.Infrastructure</c> implements it with a batch insert. The full
/// ingest endpoint + anonymizer that feed this are Step 11; the store exists now so the append path is
/// integration-testable.
/// </summary>
public interface IAnalyticsEventStore
{
    /// <summary>Appends a single anonymized event.</summary>
    Task AppendAsync(AnalyticsEvent analyticsEvent, CancellationToken cancellationToken = default);

    /// <summary>Appends a batch of anonymized events in one round-trip (the hot append path).</summary>
    Task AppendBatchAsync(IReadOnlyCollection<AnalyticsEvent> events, CancellationToken cancellationToken = default);
}
