using Microsoft.Extensions.Logging;
using WhereToEat.Admin.Application.Abstractions;
using WhereToEat.Contracts.IntegrationEvents;
using WhereToEat.Geo.Application;

namespace WhereToEat.AdminApi;

/// <summary>
/// The admin host's in-process implementation of <see cref="IAddressChangedDispatcher"/>: it projects
/// an admin address edit onto the <c>Contracts</c> <see cref="RestaurantAddressChanged"/> integration
/// event and hands it to the Step 8 <see cref="GeocodeOnAddressChangedHandler"/>, which re-geocodes the
/// restaurant. The re-geocode logic lives entirely in Step 8 — this dispatcher only emits/triggers the
/// flow, so the Admin module stays free of Geo internals (module-isolation rule d). Step 13 swaps this
/// for a MassTransit publish without touching the admin use-case.
/// </summary>
internal sealed partial class InProcessAddressChangedDispatcher : IAddressChangedDispatcher
{
    private readonly GeocodeOnAddressChangedHandler _handler;
    private readonly ILogger<InProcessAddressChangedDispatcher> _logger;

    public InProcessAddressChangedDispatcher(
        GeocodeOnAddressChangedHandler handler,
        ILogger<InProcessAddressChangedDispatcher> logger)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(logger);
        _handler = handler;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task DispatchAsync(Guid restaurantId, CancellationToken cancellationToken = default)
    {
        var @event = new RestaurantAddressChanged(
            EventId: Guid.NewGuid(),
            OccurredOnUtc: DateTimeOffset.UtcNow,
            RestaurantId: restaurantId);

        // Best-effort with respect to the admin write (the address is already persisted): the Step 8
        // handler returns a Result the worker/bus would inspect. In-process we don't fail the admin
        // write on a geocode error, but we DO log it so the failure is visible in ops until Step 13
        // moves this onto the bus (where the consumer owns retry/skip).
        var result = await _handler.HandleAsync(@event, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            LogGeocodeFailed(restaurantId, result.Error.Code, result.Error.Message);
        }
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Re-geocode after address change failed for restaurant {RestaurantId} ({ErrorCode}): {ErrorMessage}. The admin write succeeded; coordinates are stale until the next geocode.")]
    private partial void LogGeocodeFailed(Guid restaurantId, string errorCode, string errorMessage);
}
