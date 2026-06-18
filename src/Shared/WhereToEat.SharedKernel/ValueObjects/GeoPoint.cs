using WhereToEat.SharedKernel.Primitives;
using WhereToEat.SharedKernel.Results;

namespace WhereToEat.SharedKernel.ValueObjects;

/// <summary>
/// A geographic point (latitude/longitude in decimal degrees, WGS-84). Construction
/// validates the coordinate ranges so an out-of-range point can never exist. Per
/// invariant #7 these coordinates originate from OSM/Nominatim, never raw Google lat/lng —
/// that source guard lives in the Catalog domain's Coordinates type (Step 3); this
/// SharedKernel primitive is source-agnostic and only enforces numeric validity.
/// </summary>
public sealed class GeoPoint : ValueObject
{
    /// <summary>Inclusive latitude bounds in decimal degrees.</summary>
    public const double MinLatitude = -90d;
    public const double MaxLatitude = 90d;

    /// <summary>Inclusive longitude bounds in decimal degrees.</summary>
    public const double MinLongitude = -180d;
    public const double MaxLongitude = 180d;

    private GeoPoint(double latitude, double longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }

    /// <summary>Latitude in decimal degrees, within [-90, 90].</summary>
    public double Latitude { get; }

    /// <summary>Longitude in decimal degrees, within [-180, 180].</summary>
    public double Longitude { get; }

    /// <summary>
    /// Creates a <see cref="GeoPoint"/>, rejecting a latitude outside [-90, 90], a
    /// longitude outside [-180, 180], or a non-finite (NaN/Infinity) coordinate.
    /// </summary>
    public static Result<GeoPoint> Create(double latitude, double longitude)
    {
        if (!double.IsFinite(latitude) || latitude < MinLatitude || latitude > MaxLatitude)
        {
            return Result.Failure<GeoPoint>(
                Error.Validation("GeoPoint.LatitudeOutOfRange", "Latitude must be between -90 and 90 degrees."));
        }

        if (!double.IsFinite(longitude) || longitude < MinLongitude || longitude > MaxLongitude)
        {
            return Result.Failure<GeoPoint>(
                Error.Validation("GeoPoint.LongitudeOutOfRange", "Longitude must be between -180 and 180 degrees."));
        }

        return Result.Success(new GeoPoint(latitude, longitude));
    }

    /// <inheritdoc />
    public override string ToString() =>
        $"({Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}, " +
        $"{Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)})";

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Latitude;
        yield return Longitude;
    }
}
