using MassTransit;
using Microsoft.Extensions.Logging;
using WhereToEat.Contracts.Geo;
using WhereToEat.Contracts.IntegrationEvents;

namespace WhereToEat.BuildingBlocks.Messaging.Consumers;

/// <summary>
/// Reacts to a <see cref="RestaurantAddressChanged"/> integration event by re-geocoding the restaurant.
/// It delegates to the Contracts <see cref="IRestaurantGeocoder"/> seam (the Geo module supplies the
/// adapter over its Step 8 geocode flow), so this consumer depends <b>only on <c>WhereToEat.Contracts</c></b>
/// across module lines — never on the Geo module's <c>IGeocoder</c>/command-handler internals. That keeps
/// the Step 2 consumer-boundary fitness rule (e) green.
/// <para>
/// Best-effort: a failed geocode does not fault the message (the address change is already persisted;
/// only the coordinate is missing until a later run), it is logged and acknowledged.
/// </para>
/// </summary>
public sealed partial class GeocodeOnAddressChangedConsumer : IConsumer<RestaurantAddressChanged>
{
    private readonly IRestaurantGeocoder _geocoder;
    private readonly ILogger<GeocodeOnAddressChangedConsumer> _logger;

    public GeocodeOnAddressChangedConsumer(
        IRestaurantGeocoder geocoder,
        ILogger<GeocodeOnAddressChangedConsumer> logger)
    {
        ArgumentNullException.ThrowIfNull(geocoder);
        ArgumentNullException.ThrowIfNull(logger);
        _geocoder = geocoder;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<RestaurantAddressChanged> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var restaurantId = context.Message.RestaurantId;

        var refreshed = await _geocoder
            .GeocodeAndStoreAsync(restaurantId, context.CancellationToken)
            .ConfigureAwait(false);

        if (refreshed)
        {
            LogGeocoded(restaurantId);
        }
        else
        {
            LogNoCoordinate(restaurantId);
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "Re-geocoded restaurant {RestaurantId} after an address change.")]
    private partial void LogGeocoded(Guid restaurantId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning,
        Message = "Address change for restaurant {RestaurantId} produced no usable coordinate; will retry on a later run.")]
    private partial void LogNoCoordinate(Guid restaurantId);
}
