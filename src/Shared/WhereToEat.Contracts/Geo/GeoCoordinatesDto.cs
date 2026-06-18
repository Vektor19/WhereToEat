namespace WhereToEat.Contracts.Geo;

/// <summary>
/// The cross-module, transport-shaped result of a geocode: an <b>OSM/Nominatim-sourced</b>
/// latitude/longitude plus the optional Google <b>Place ID</b> and Maps deep-link (not coordinates —
/// allowed to store). This is the only coordinate shape that crosses a module boundary; it carries a
/// plain lat/lng pair (validated when the catalog rebuilds its <c>GeoPoint</c>/<c>Coordinates</c>),
/// and there is deliberately <b>no</b> field for a "Google coordinate" — invariant #7 holds because
/// the only producer is the OSM/Nominatim geocoder and the only consumer rebuilds the OSM-only
/// <c>Coordinates</c> value object.
/// </summary>
public sealed record GeoCoordinatesDto(
    double Latitude,
    double Longitude,
    string? PlaceId = null,
    string? MapsDeepLink = null);
