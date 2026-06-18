namespace WhereToEat.Analytics.Application;

/// <summary>
/// The ingest command for one or more raw analytics events arriving at <c>POST /analytics/events</c>.
/// It carries the <b>raw</b> payloads only as far as the handler, which anonymizes them <b>before</b>
/// anything is persisted (§8.1 / invariant #11). Accepting a batch keeps the high-volume client→server
/// hop cheap and maps straight onto the append-optimized batch writer.
/// </summary>
public sealed record IngestEventCommand(IReadOnlyList<RawAnalyticsEvent> Events);
