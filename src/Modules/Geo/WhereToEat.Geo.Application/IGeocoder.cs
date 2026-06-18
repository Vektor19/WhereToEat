using WhereToEat.Contracts.Geo;
using WhereToEat.SharedKernel.Results;

namespace WhereToEat.Geo.Application;

/// <summary>
/// The geocoder port (Step 8): turns a free-form address string into coordinates we are allowed to
/// store under ODbL (OSM/Nominatim). The concrete adapter (<c>NominatimGeocoder</c> in
/// Geo.Infrastructure) calls Nominatim over HTTP honouring its usage policy (descriptive User-Agent,
/// a configured minimum request interval, attribution).
///
/// The result is the OSM-sourced <see cref="GeoCoordinatesDto"/> — never a Google coordinate
/// (invariant #7). A no-result / transport / parse failure surfaces as a failure <see cref="Result{T}"/>
/// rather than an exception or a fabricated point, so callers can decide whether to retry or skip.
/// </summary>
public interface IGeocoder
{
    /// <summary>
    /// Geocodes <paramref name="address"/> to an OSM-sourced coordinate. Returns a failure
    /// <see cref="Result{T}"/> on a blank address, an empty/no-match response, or a transport error.
    /// </summary>
    Task<Result<GeoCoordinatesDto>> GeocodeAsync(string address, CancellationToken cancellationToken = default);
}
