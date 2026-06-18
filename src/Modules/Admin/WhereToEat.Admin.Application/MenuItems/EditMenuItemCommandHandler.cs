using Microsoft.Extensions.Logging;
using WhereToEat.Admin.Application.Abstractions;
using WhereToEat.SharedKernel.Results;

namespace WhereToEat.Admin.Application.MenuItems;

/// <summary>
/// Applies an <see cref="EditMenuItemCommand"/> through the EF-backed <see cref="IAdminCatalogStore"/>.
/// The handler is thin — validation guards live in the store/domain — so the use-case stays
/// unit-testable with a fake store.
/// </summary>
public sealed partial class EditMenuItemCommandHandler
{
    private readonly IAdminCatalogStore _store;
    private readonly ILogger<EditMenuItemCommandHandler> _logger;

    public EditMenuItemCommandHandler(IAdminCatalogStore store, ILogger<EditMenuItemCommandHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(logger);
        _store = store;
        _logger = logger;
    }

    /// <summary>Edits the menu item; returns the store's <see cref="Result"/>.</summary>
    public async Task<Result> HandleAsync(EditMenuItemCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var result = await _store
            .EditMenuItemAsync(
                command.MenuItemId,
                command.DishId,
                command.PriceAmount,
                command.PriceCurrency,
                command.Weight,
                cancellationToken)
            .ConfigureAwait(false);

        if (result.IsSuccess)
        {
            LogEdited(command.MenuItemId);
        }

        return result;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "Admin edited menu item {MenuItemId} (price/weight/dish); marked admin-sourced.")]
    private partial void LogEdited(Guid menuItemId);
}
