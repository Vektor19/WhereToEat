using WhereToEat.SharedKernel.ValueObjects;

namespace WhereToEat.SharedKernel.Geo;

/// <summary>
/// Great-circle distance between two <see cref="GeoPoint"/>s using the haversine formula.
/// This is invariant #7's local distance math — "X km from you" computed in code with no
/// paid geo API. Pure and framework-free; the SQL <c>geography</c> spatial index only
/// narrows candidates, and this decides exact distance/ordering.
/// </summary>
public static class Haversine
{
    /// <summary>Mean Earth radius in kilometres (IUGG mean radius R1).</summary>
    public const double EarthRadiusKm = 6371.0088d;

    /// <summary>
    /// Returns the great-circle distance between <paramref name="from"/> and
    /// <paramref name="to"/> in kilometres. Distance from a point to itself is exactly 0.
    /// </summary>
    public static double DistanceKm(GeoPoint from, GeoPoint to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        // Identity short-circuit guarantees an exact 0.0 (no floating-point residue).
        if (from.Latitude == to.Latitude && from.Longitude == to.Longitude)
        {
            return 0d;
        }

        var lat1 = DegreesToRadians(from.Latitude);
        var lat2 = DegreesToRadians(to.Latitude);
        var deltaLat = DegreesToRadians(to.Latitude - from.Latitude);
        var deltaLon = DegreesToRadians(to.Longitude - from.Longitude);

        var sinHalfDeltaLat = Math.Sin(deltaLat / 2d);
        var sinHalfDeltaLon = Math.Sin(deltaLon / 2d);

        var a = (sinHalfDeltaLat * sinHalfDeltaLat)
            + (Math.Cos(lat1) * Math.Cos(lat2) * sinHalfDeltaLon * sinHalfDeltaLon);

        var c = 2d * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1d - a));

        return EarthRadiusKm * c;
    }

    private static double DegreesToRadians(double degrees) => degrees * (Math.PI / 180d);
}
