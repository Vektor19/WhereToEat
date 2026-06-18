namespace WhereToEat.Contracts.IntegrationEvents;

/// <summary>
/// Published when a user submits a rating for a restaurant. The recommendation module's
/// rating aggregate / smoothed value is materialized from these (via the DB), so the
/// engine never references the Ratings domain directly (Step 7 cross-module rule).
/// </summary>
public sealed record RatingGiven(
    Guid EventId,
    DateTimeOffset OccurredOnUtc,
    Guid RestaurantId,
    int Score) : IIntegrationEvent;
