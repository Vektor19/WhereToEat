using WhereToEat.SharedKernel.Results;
using WhereToEat.SharedKernel.Primitives;
using WhereToEat.SharedKernel.ValueObjects;

namespace WhereToEat.Catalog.Domain.Restaurants;

/// <summary>
/// A restaurant's spatial data (a value object): the OSM/Nominatim-sourced <see cref="GeoPoint"/>,
/// an optional Google <b>Place ID</b>, and an optional Google Maps <b>deep-link</b>.
///
/// Invariant #7 is encoded structurally here: the only spatial factory is
/// <see cref="FromOsmGeoPoint"/>, which takes a <see cref="GeoPoint"/> we are allowed to store
/// under ODbL (OSM/Nominatim). There is deliberately <b>no</b> "from Google coordinates" factory —
/// raw Google lat/lng must never be stored, so the type offers no code path to construct one. Only
/// the non-coordinate Google identifiers (Place ID, deep-link) are accepted, which the rules permit.
/// </summary>
public sealed class Coordinates : ValueObject
{
    private Coordinates(GeoPoint point, string? placeId, string? mapsDeepLink)
    {
        Point = point;
        PlaceId = placeId;
        MapsDeepLink = mapsDeepLink;
    }

    /// <summary>The OSM/Nominatim-sourced point. Never originates from Google lat/lng (invariant #7).</summary>
    public GeoPoint Point { get; }

    /// <summary>The Google Maps Place ID, when known. Allowed to store indefinitely; not a coordinate.</summary>
    public string? PlaceId { get; }

    /// <summary>A Google Maps deep-link for "open in Maps", when known. Not a coordinate.</summary>
    public string? MapsDeepLink { get; }

    /// <summary>
    /// Creates <see cref="Coordinates"/> from an <b>OSM/Nominatim-sourced</b> <see cref="GeoPoint"/>
    /// (the only spatial source the model permits — invariant #7). The Place ID and deep-link are
    /// optional Google identifiers (not coordinates); blank values are normalised to <c>null</c>.
    /// This is intentionally the sole spatial factory: no overload accepts "Google coordinates".
    /// </summary>
    public static Result<Coordinates> FromOsmGeoPoint(
        GeoPoint point,
        string? placeId = null,
        string? mapsDeepLink = null)
    {
        if (point is null)
        {
            return Result.Failure<Coordinates>(
                Error.Validation("Coordinates.PointRequired", "A coordinate point is required."));
        }

        var normalizedPlaceId = string.IsNullOrWhiteSpace(placeId) ? null : placeId.Trim();
        var normalizedDeepLink = string.IsNullOrWhiteSpace(mapsDeepLink) ? null : mapsDeepLink.Trim();

        return Result.Success(new Coordinates(point, normalizedPlaceId, normalizedDeepLink));
    }

    /// <summary>Returns a copy with the Place ID and/or deep-link set, keeping the same OSM point.</summary>
    public Coordinates WithGoogleIdentifiers(string? placeId, string? mapsDeepLink)
    {
        var normalizedPlaceId = string.IsNullOrWhiteSpace(placeId) ? null : placeId.Trim();
        var normalizedDeepLink = string.IsNullOrWhiteSpace(mapsDeepLink) ? null : mapsDeepLink.Trim();
        return new Coordinates(Point, normalizedPlaceId, normalizedDeepLink);
    }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Point;
        yield return PlaceId;
        yield return MapsDeepLink;
    }
}
