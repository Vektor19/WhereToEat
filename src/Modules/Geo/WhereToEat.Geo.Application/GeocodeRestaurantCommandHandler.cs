using Microsoft.Extensions.Logging;
using WhereToEat.Contracts.Geo;
using WhereToEat.SharedKernel.Results;

namespace WhereToEat.Geo.Application;

/// <summary>
/// Handles <see cref="GeocodeRestaurantCommand"/>: reads the restaurant's current address through the
/// Contracts <see cref="ICatalogCoordinateWriter"/> seam, geocodes it via <see cref="IGeocoder"/>, and
/// writes the OSM-sourced coordinates back through the same seam. This is the only place the geocode
/// flow joins Catalog, and it does so purely through Contracts — so the Geo module never references
/// Catalog internals. The handler stores only the OSM point + optional Place ID/deep-link the geocoder
/// returned; there is no path that writes a Google coordinate (invariant #7).
///
/// Failures are returned as a failure <see cref="Result"/> (unknown restaurant, no geocode match,
/// transport error) rather than thrown, so the worker/handler can log-and-skip without aborting a
/// batch.
/// </summary>
public sealed partial class GeocodeRestaurantCommandHandler
{
    private readonly IGeocoder _geocoder;
    private readonly ICatalogCoordinateWriter _coordinateWriter;
    private readonly ILogger<GeocodeRestaurantCommandHandler> _logger;

    public GeocodeRestaurantCommandHandler(
        IGeocoder geocoder,
        ICatalogCoordinateWriter coordinateWriter,
        ILogger<GeocodeRestaurantCommandHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(geocoder);
        ArgumentNullException.ThrowIfNull(coordinateWriter);
        ArgumentNullException.ThrowIfNull(logger);
        _geocoder = geocoder;
        _coordinateWriter = coordinateWriter;
        _logger = logger;
    }

    /// <summary>
    /// Executes the command. Returns success when the restaurant's stored coordinates were refreshed;
    /// a failure <see cref="Result"/> otherwise (no such restaurant, no geocode match, write rejected).
    /// </summary>
    public async Task<Result> HandleAsync(GeocodeRestaurantCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var address = await _coordinateWriter
            .GetGeocodingAddressAsync(command.RestaurantId, cancellationToken)
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(address))
        {
            LogNoAddress(command.RestaurantId);
            return Result.Failure(
                Error.NotFound(
                    "Geocode.RestaurantNotFound",
                    "No restaurant with a geocodable address was found for the given id."));
        }

        var geocodeResult = await _geocoder.GeocodeAsync(address, cancellationToken).ConfigureAwait(false);
        if (geocodeResult.IsFailure)
        {
            LogGeocodeFailed(command.RestaurantId, geocodeResult.Error.Code, geocodeResult.Error.Message);
            return Result.Failure(geocodeResult.Error);
        }

        var persisted = await _coordinateWriter
            .SetCoordinatesAsync(command.RestaurantId, geocodeResult.Value, cancellationToken)
            .ConfigureAwait(false);

        if (!persisted)
        {
            LogWriteRejected(command.RestaurantId);
            return Result.Failure(
                Error.Failure(
                    "Geocode.WriteRejected",
                    "The geocoded coordinates could not be persisted for the restaurant."));
        }

        LogRefreshed(command.RestaurantId);
        return Result.Success();
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Geocode skipped: restaurant {RestaurantId} has no address to geocode (missing or unknown).")]
    private partial void LogNoAddress(Guid restaurantId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "Geocode failed for restaurant {RestaurantId}: {ErrorCode} {ErrorMessage}.")]
    private partial void LogGeocodeFailed(Guid restaurantId, string errorCode, string errorMessage);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "Geocode produced coordinates for restaurant {RestaurantId} but the write was rejected " +
            "(restaurant vanished or the coordinate was out of range).")]
    private partial void LogWriteRejected(Guid restaurantId);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Information,
        Message = "Refreshed coordinates for restaurant {RestaurantId}.")]
    private partial void LogRefreshed(Guid restaurantId);
}
