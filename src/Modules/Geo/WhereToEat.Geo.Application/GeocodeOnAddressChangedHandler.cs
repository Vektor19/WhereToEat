using Microsoft.Extensions.Logging;
using WhereToEat.Contracts.IntegrationEvents;
using WhereToEat.SharedKernel.Results;

namespace WhereToEat.Geo.Application;

/// <summary>
/// The <c>AddressChanged → re-geocode</c> seam (deferred from Step 6, owned end-to-end here): on a
/// <see cref="RestaurantAddressChanged"/> event, it re-geocodes the restaurant by delegating to the
/// <see cref="GeocodeRestaurantCommandHandler"/> so its stored coordinates are refreshed from the new
/// address. It depends only on the <c>Contracts</c> event shape — never on the Catalog domain's
/// internal <c>AddressChanged</c> domain event — so when Step 13 turns this into a MassTransit
/// consumer the Step 2 consumer-boundary fitness rule already holds.
///
/// Step 12's worker drives the geocode on a schedule/queue and Step 13 carries the event over the
/// bus; this handler is the in-module reaction both of those wire up to.
/// </summary>
public sealed partial class GeocodeOnAddressChangedHandler
{
    private readonly GeocodeRestaurantCommandHandler _geocodeHandler;
    private readonly ILogger<GeocodeOnAddressChangedHandler> _logger;

    public GeocodeOnAddressChangedHandler(
        GeocodeRestaurantCommandHandler geocodeHandler,
        ILogger<GeocodeOnAddressChangedHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(geocodeHandler);
        ArgumentNullException.ThrowIfNull(logger);
        _geocodeHandler = geocodeHandler;
        _logger = logger;
    }

    /// <summary>
    /// Re-geocodes the restaurant named by <paramref name="event"/>. Returns the geocode
    /// <see cref="Result"/> so the caller (worker/bus consumer) can decide its retry/skip policy.
    /// </summary>
    public async Task<Result> HandleAsync(RestaurantAddressChanged @event, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);

        LogReGeocoding(@event.RestaurantId, @event.EventId);

        return await _geocodeHandler
            .HandleAsync(new GeocodeRestaurantCommand(@event.RestaurantId), cancellationToken)
            .ConfigureAwait(false);
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Address changed for restaurant {RestaurantId}; re-geocoding (event {EventId}).")]
    private partial void LogReGeocoding(Guid restaurantId, Guid eventId);
}
