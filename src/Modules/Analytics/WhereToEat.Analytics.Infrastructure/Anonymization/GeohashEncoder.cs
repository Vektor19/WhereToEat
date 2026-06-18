using System.Text;

namespace WhereToEat.Analytics.Infrastructure.Anonymization;

/// <summary>
/// Encodes a precise lat/lng into a <b>coarse, neighbourhood-grade</b> geohash so analytics retains
/// only an approximate area, never a point (§8.1 / invariant #11). Standard public-domain geohash
/// base-32 interleaving, truncated to a configurable character count: fewer chars ⇒ a larger cell.
/// The default precision is capped at the domain's <c>Geohash.MaxPrecision</c> so a produced hash is
/// always coarse enough to clear the privacy floor.
/// </summary>
public static class GeohashEncoder
{
    private const string Base32 = "0123456789bcdefghjkmnpqrstuvwxyz";

    /// <summary>
    /// Encodes (<paramref name="latitude"/>, <paramref name="longitude"/>) to a geohash of
    /// <paramref name="precision"/> base-32 characters. Precision must be 1.. (a small count keeps the
    /// cell neighbourhood-grade); the caller is expected to pass a coarse value.
    /// </summary>
    public static string Encode(double latitude, double longitude, int precision)
    {
        if (precision < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(precision), precision, "Geohash precision must be at least 1.");
        }

        var latRange = (Min: -90.0, Max: 90.0);
        var lonRange = (Min: -180.0, Max: 180.0);

        var geohash = new StringBuilder(precision);
        var isEven = true; // Even bits split longitude; odd bits split latitude (standard geohash).
        var bit = 0;
        var ch = 0;

        while (geohash.Length < precision)
        {
            if (isEven)
            {
                var mid = (lonRange.Min + lonRange.Max) / 2;
                if (longitude >= mid)
                {
                    ch |= 1 << (4 - bit);
                    lonRange.Min = mid;
                }
                else
                {
                    lonRange.Max = mid;
                }
            }
            else
            {
                var mid = (latRange.Min + latRange.Max) / 2;
                if (latitude >= mid)
                {
                    ch |= 1 << (4 - bit);
                    latRange.Min = mid;
                }
                else
                {
                    latRange.Max = mid;
                }
            }

            isEven = !isEven;

            if (bit < 4)
            {
                bit++;
            }
            else
            {
                geohash.Append(Base32[ch]);
                bit = 0;
                ch = 0;
            }
        }

        return geohash.ToString();
    }
}
