using Microsoft.Extensions.Logging;
using WhereToEat.Admin.Application.Abstractions;
using WhereToEat.SharedKernel.Results;

namespace WhereToEat.Admin.Application.Protection;

/// <summary>
/// Applies a <see cref="SetRestaurantDoNotUpdateCommand"/> through the EF-backed
/// <see cref="IAdminCatalogStore"/>. This is the venue-level protection write the Step 9 parser gate
/// observes (invariant #3, admin &gt; parser).
/// </summary>
public sealed partial class SetRestaurantDoNotUpdateCommandHandler
{
    private readonly IAdminCatalogStore _store;
    private readonly ILogger<SetRestaurantDoNotUpdateCommandHandler> _logger;

    public SetRestaurantDoNotUpdateCommandHandler(IAdminCatalogStore store, ILogger<SetRestaurantDoNotUpdateCommandHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(logger);
        _store = store;
        _logger = logger;
    }

    /// <summary>Sets the flag; returns the store's <see cref="Result"/>.</summary>
    public async Task<Result> HandleAsync(SetRestaurantDoNotUpdateCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var result = await _store
            .SetRestaurantDoNotUpdateAsync(command.RestaurantId, command.DoNotUpdate, cancellationToken)
            .ConfigureAwait(false);

        if (result.IsSuccess)
        {
            LogSet(command.RestaurantId, command.DoNotUpdate);
        }

        return result;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "Admin set DoNotUpdate={DoNotUpdate} on restaurant {RestaurantId} (admin > parser).")]
    private partial void LogSet(Guid restaurantId, bool doNotUpdate);
}
