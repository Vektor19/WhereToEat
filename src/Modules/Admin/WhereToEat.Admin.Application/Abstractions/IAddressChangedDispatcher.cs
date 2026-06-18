namespace WhereToEat.Admin.Application.Abstractions;

/// <summary>
/// The seam by which an admin address edit (Step 10) triggers the Step 8 re-geocode flow. The
/// Catalog domain's <c>AddressChanged</c> domain event is projected onto the <c>Contracts</c>
/// integration event <c>RestaurantAddressChanged</c>, which the Step 8
/// <c>GeocodeOnAddressChangedHandler</c> consumes. The re-geocode logic itself lives in Step 8 — this
/// port only emits/triggers it, so the Admin module never references Geo internals (module-isolation
/// rule d). Step 13 swaps the dispatcher to publish over the MassTransit bus without touching the
/// admin use-case; today the host wires an in-process dispatcher that invokes the Step 8 handler.
/// </summary>
public interface IAddressChangedDispatcher
{
    /// <summary>
    /// Dispatches the address-changed signal for <paramref name="restaurantId"/> so the Geo module
    /// re-geocodes it from the new address. Best-effort with respect to the admin write: the address
    /// is already persisted by the time this is called, so a geocode failure is the Geo flow's
    /// concern, not the edit's.
    /// </summary>
    Task DispatchAsync(Guid restaurantId, CancellationToken cancellationToken = default);
}
