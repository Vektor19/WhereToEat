using System.Globalization;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using WhereToEat.Contracts.Geo;
using WhereToEat.Geo.Application;
using WhereToEat.SharedKernel.Results;
using WhereToEat.SharedKernel.ValueObjects;

namespace WhereToEat.Geo.Infrastructure;

/// <summary>
/// The OSM/<b>Nominatim</b> adapter behind <see cref="IGeocoder"/>. It calls Nominatim's
/// <c>/search?format=jsonv2&amp;limit=1</c> over the injected (typed) <see cref="HttpClient"/>,
/// honouring Nominatim's usage policy:
/// <list type="bullet">
///   <item>a <b>descriptive User-Agent</b> on every request (set by the typed-client registration);</item>
///   <item>a configured <b>minimum request interval</b> (rate-limit) — enforced by the process-wide
///     <see cref="RateLimitingHandler"/>/<see cref="NominatimRateLimiter"/> in the HTTP pipeline, so it
///     covers every request including Polly retries (not just the first), and is shared across all
///     callers in the process — <b>not</b> a per-instance gate;</item>
///   <item>attribution carried in <see cref="NominatimOptions.Attribution"/> for stored coordinates.</item>
/// </list>
/// A successful response maps into the OSM-sourced <see cref="GeoCoordinatesDto"/> (lat/lon parsed
/// invariant-culture). There is deliberately <b>no</b> code path that reads or writes a Google
/// coordinate (invariant #7). An empty/no-match response, an out-of-range coordinate, an unparsable
/// body, or a transport error all surface as a failure <see cref="Result{T}"/> — never a thrown
/// exception out of <see cref="GeocodeAsync"/> or a fabricated point. The geocoder holds <b>no</b> rate
/// state itself, so it is free to be a transient typed client.
/// </summary>
public sealed partial class NominatimGeocoder : IGeocoder
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<NominatimGeocoder> _logger;

    public NominatimGeocoder(
        HttpClient httpClient,
        ILogger<NominatimGeocoder> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(logger);
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<GeoCoordinatesDto>> GeocodeAsync(string address, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return Result.Failure<GeoCoordinatesDto>(
                Error.Validation("Geocode.AddressRequired", "An address to geocode cannot be blank."));
        }

        var requestUri = $"search?format=jsonv2&limit=1&q={Uri.EscapeDataString(address.Trim())}";

        try
        {
            // The rate-limit is enforced by the pipeline's RateLimitingHandler (process-wide, every
            // request including Polly retries), so the geocoder just sends.
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                LogUpstreamStatus((int)response.StatusCode, address);
                return Result.Failure<GeoCoordinatesDto>(
                    Error.Failure(
                        "Geocode.UpstreamError",
                        $"The geocoding service returned status {(int)response.StatusCode}."));
            }

            var results = await response.Content
                .ReadFromJsonAsync<IReadOnlyList<NominatimResult>>(cancellationToken)
                .ConfigureAwait(false);

            return MapFirstResult(results, address);
        }
        catch (HttpRequestException ex)
        {
            LogTransportError(ex, address);
            return Result.Failure<GeoCoordinatesDto>(
                Error.Failure("Geocode.TransportError", "The geocoding request failed to reach the service."));
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // A timeout (Polly/HttpClient), not a caller cancellation.
            LogTimeout(ex, address);
            return Result.Failure<GeoCoordinatesDto>(
                Error.Failure("Geocode.Timeout", "The geocoding request timed out."));
        }
    }

    private Result<GeoCoordinatesDto> MapFirstResult(IReadOnlyList<NominatimResult>? results, string address)
    {
        var first = results is { Count: > 0 } ? results[0] : null;
        if (first is null
            || !double.TryParse(first.Lat, NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude)
            || !double.TryParse(first.Lon, NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude))
        {
            LogNoMatch(address);
            return Result.Failure<GeoCoordinatesDto>(
                Error.NotFound("Geocode.NoMatch", "The address could not be geocoded to a coordinate."));
        }

        // Validate the coordinate through the SharedKernel GeoPoint (the same numeric guard the
        // catalog Coordinates value object uses), so a malformed upstream value can never propagate.
        var pointResult = GeoPoint.Create(latitude, longitude);
        if (pointResult.IsFailure)
        {
            LogOutOfRange(latitude, longitude, address);
            return Result.Failure<GeoCoordinatesDto>(pointResult.Error);
        }

        // OSM-sourced coordinate only — no Place ID/deep-link comes from Nominatim (invariant #7).
        return Result.Success(new GeoCoordinatesDto(latitude, longitude));
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Nominatim returned {StatusCode} geocoding '{Address}'.")]
    private partial void LogUpstreamStatus(int statusCode, string address);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Nominatim returned no usable match for '{Address}'.")]
    private partial void LogNoMatch(string address);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "Nominatim returned an out-of-range coordinate ({Latitude}, {Longitude}) for '{Address}'.")]
    private partial void LogOutOfRange(double latitude, double longitude, string address);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning, Message = "Transport error geocoding '{Address}'.")]
    private partial void LogTransportError(Exception exception, string address);

    [LoggerMessage(EventId = 5, Level = LogLevel.Warning, Message = "Timeout geocoding '{Address}'.")]
    private partial void LogTimeout(Exception exception, string address);
}
