namespace WhereToEat.Geo.Application;

/// <summary>
/// Command to (re)geocode a single restaurant: look up its current address, geocode it through the
/// <see cref="IGeocoder"/> port, and persist the OSM-sourced coordinates back onto the restaurant via
/// the Contracts coordinate-writer seam. Driven by the Step 12 worker (geocode-refresh job) and by
/// the <see cref="GeocodeOnAddressChangedHandler"/> when an address changes. Carries only the
/// restaurant id — the current address is read through the catalog seam, so the command never holds a
/// stale address.
/// </summary>
public sealed record GeocodeRestaurantCommand(Guid RestaurantId);
