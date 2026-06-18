namespace WhereToEat.Catalog.Application.Contracts;

/// <summary>
/// The "Глянути на карті" (§5.7) Map payload, assembled <b>only</b> from fields already stored on
/// the restaurant: our OSM-sourced coordinates, the optional Google Place ID, and the optional
/// Maps deep-link. It is live-only and <b>nothing is cached</b> (§5.7); no Google rating and no
/// Google-sourced coordinate ever appear here (invariants #6/#7).
///
/// <para>
/// <see cref="HasMapData"/> is <c>false</c> when the restaurant has no stored coordinates — the
/// endpoint returns this "no map payload" shape rather than attempting to geocode (the geocoder
/// port does not exist until Step 8). When false, the coordinate fields are <c>null</c>.
/// </para>
/// </summary>
public sealed record MapPayloadDto(
    Guid RestaurantId,
    bool HasMapData,
    double? Latitude,
    double? Longitude,
    string? PlaceId,
    string? MapsDeepLink)
{
    /// <summary>The well-defined "no stored coordinates" payload — nothing to map, no geocoding.</summary>
    public static MapPayloadDto NoMapData(Guid restaurantId)
        => new(restaurantId, HasMapData: false, Latitude: null, Longitude: null, PlaceId: null, MapsDeepLink: null);
}
