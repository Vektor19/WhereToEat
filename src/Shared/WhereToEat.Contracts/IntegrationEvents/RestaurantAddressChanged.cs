namespace WhereToEat.Contracts.IntegrationEvents;

/// <summary>
/// Published when a restaurant's address has changed (admin edit in Step 10), so the Geo module can
/// re-geocode it. This is the <b>Contracts</b> projection of the Catalog domain's <c>AddressChanged</c>
/// domain event: the Geo handler/consumer depends only on this contract, never on the Catalog
/// domain's internals (the Step 2 consumer-boundary fitness rule). Step 13 carries it over the bus;
/// the geocode-on-address-changed handler that consumes it first appears in Step 8. Carries only the
/// restaurant id — the geocoder reads the current address through the catalog coordinate-writer port.
/// </summary>
public sealed record RestaurantAddressChanged(
    Guid EventId,
    DateTimeOffset OccurredOnUtc,
    Guid RestaurantId) : IIntegrationEvent;
