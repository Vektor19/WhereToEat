namespace WhereToEat.Analytics.Infrastructure.Persistence;

/// <summary>
/// The flat row shape Dapper materializes from <c>analytics.AnalyticsEvent</c>. There is no column
/// for a raw user/session id, a precise coordinate, or a minute-grade timestamp — by schema, only the
/// anonymized pieces exist. Internal — a persistence detail used by the read-back path.
/// </summary>
internal sealed class AnalyticsEventRow
{
    public Guid Id { get; init; }

    public int Kind { get; init; }

    public DateTimeOffset OccurredAtHour { get; init; }

    public string? ActorHash { get; init; }

    public string? CoarseGeohash { get; init; }

    public string DimensionsJson { get; init; } = "{}";
}
