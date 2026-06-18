namespace WhereToEat.Admin.Application.Addresses;

/// <summary>
/// Admin edit of a restaurant's address (§4). Persisting a new address raises the Step 3
/// <c>AddressChanged</c> flow — the address is the geocoder's input, so a change must trigger fresh
/// OSM/Nominatim coordinates (invariant #7). The re-geocode handler itself lives in Step 8; this
/// command only emits/triggers it through the <c>IAddressChangedDispatcher</c> seam.
/// </summary>
public sealed record EditAddressCommand(Guid RestaurantId, string AddressLine, string? City);
