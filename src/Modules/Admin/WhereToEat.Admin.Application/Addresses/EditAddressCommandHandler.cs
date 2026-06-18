using Microsoft.Extensions.Logging;
using WhereToEat.Admin.Application.Abstractions;
using WhereToEat.SharedKernel.Results;

namespace WhereToEat.Admin.Application.Addresses;

/// <summary>
/// Applies an <see cref="EditAddressCommand"/>: persists the new address through the EF-backed
/// <see cref="IAdminCatalogStore"/> and — only when the write succeeded — raises the re-geocode flow
/// through the <see cref="IAddressChangedDispatcher"/> seam (the Step 8 handler consumes it). The
/// dispatch happens AFTER a successful persist so we never re-geocode an address that did not change
/// (e.g. an unknown restaurant or a rejected edit).
/// </summary>
public sealed partial class EditAddressCommandHandler
{
    private readonly IAdminCatalogStore _store;
    private readonly IAddressChangedDispatcher _dispatcher;
    private readonly ILogger<EditAddressCommandHandler> _logger;

    public EditAddressCommandHandler(
        IAdminCatalogStore store,
        IAddressChangedDispatcher dispatcher,
        ILogger<EditAddressCommandHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(logger);
        _store = store;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    /// <summary>
    /// Edits the address; on success raises <c>AddressChanged</c> → re-geocode (Step 8 seam). Returns
    /// the store's <see cref="Result"/> — the geocode runs after a successful edit.
    /// </summary>
    public async Task<Result> HandleAsync(EditAddressCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var result = await _store
            .EditAddressAsync(command.RestaurantId, command.AddressLine, command.City, cancellationToken)
            .ConfigureAwait(false);

        if (result.IsFailure)
        {
            return result;
        }

        // The address is now persisted; raise the Step 3 AddressChanged flow so the Step 8 re-geocode
        // handler refreshes the stored coordinates (invariant #7). The dispatcher is the seam Step 13
        // later moves onto the bus.
        LogAddressChanged(command.RestaurantId);
        await _dispatcher.DispatchAsync(command.RestaurantId, cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "Admin edited address for restaurant {RestaurantId}; raising AddressChanged → re-geocode (Step 8).")]
    private partial void LogAddressChanged(Guid restaurantId);
}
