namespace WhereToEat.Contracts.IntegrationEvents;

/// <summary>
/// Published when a restaurant's menu has been (re)written — e.g. after a parse run or an
/// admin edit — so interested modules (analytics, the price-median refresh, caches) can
/// react. Carries only ids/counts, never another module's domain objects.
/// </summary>
public sealed record MenuUpdated(
    Guid EventId,
    DateTimeOffset OccurredOnUtc,
    Guid RestaurantId,
    int AffectedMenuItemCount) : IIntegrationEvent;
