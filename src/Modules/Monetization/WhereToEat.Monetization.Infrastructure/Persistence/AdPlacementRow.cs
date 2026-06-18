namespace WhereToEat.Monetization.Infrastructure.Persistence;

/// <summary>
/// The flat row shape Dapper materializes from <c>monetization.AdPlacement</c> before
/// <see cref="DapperAdPlacementRepository"/> rehydrates it into the domain aggregate. Kept internal —
/// it is a persistence detail. Note (invariant #10) there is no rank/boost/score column to map: an ad
/// placement carries only its identity, the venue, the targeting key, and the active window.
/// </summary>
internal sealed class AdPlacementRow
{
    public Guid Id { get; init; }

    public Guid VenueId { get; init; }

    public string TargetingKey { get; init; } = string.Empty;

    public DateTimeOffset StartsAt { get; init; }

    public DateTimeOffset EndsAt { get; init; }
}
