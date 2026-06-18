using System.Globalization;
using WhereToEat.Contracts.Recommendation;

namespace WhereToEat.Recommendation.Infrastructure.Medians;

/// <summary>
/// Derives the coarse area key a price median is scoped to from the user's location. It rounds
/// lat/lng to a coarse grid (neighbourhood-grade, not point-grade — consistent with the privacy
/// posture of using only approximate location) so the same neighbourhood maps to the same key the
/// Step 12 nightly job writes. When no location is supplied the key is the city-wide sentinel, so
/// <c>f_price</c> still gets a market reference (the fallback row).
/// </summary>
internal static class AreaKey
{
    // ~0.05° ≈ a few km — a coarse neighbourhood cell. The Step 12 nightly writer
    // (PriceMedianCalculator) resolves the SAME grid via FromCoordinates so the keys it writes are
    // exactly the keys this provider reads back; kept here as the single definition both sides share.
    private const double GridDegrees = 0.05d;

    /// <summary>Returns the coarse area key for <paramref name="userGeo"/>, or the city-wide sentinel when null.</summary>
    public static string Resolve(UserGeo? userGeo)
    {
        if (userGeo is null)
        {
            return RecommendationSql.CityWideAreaKey;
        }

        return FromCoordinates(userGeo.Latitude, userGeo.Longitude);
    }

    /// <summary>
    /// Returns the coarse area key for a restaurant's stored coordinates. The Step 12 nightly job
    /// groups menu prices by this key, so the per-area rows it writes are read back by the exact same
    /// grid the recommendation request resolves through <see cref="Resolve"/> — the two never drift.
    /// </summary>
    public static string FromCoordinates(double latitude, double longitude)
    {
        var latCell = Math.Floor(latitude / GridDegrees);
        var lngCell = Math.Floor(longitude / GridDegrees);

        // A compact, stable key per cell. Invariant culture keeps the format platform-independent.
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0}:{1}",
            (int)latCell,
            (int)lngCell);
    }
}
