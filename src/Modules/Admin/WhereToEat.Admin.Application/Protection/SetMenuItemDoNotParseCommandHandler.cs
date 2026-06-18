using Microsoft.Extensions.Logging;
using WhereToEat.Admin.Application.Abstractions;
using WhereToEat.SharedKernel.Results;

namespace WhereToEat.Admin.Application.Protection;

/// <summary>
/// Applies a <see cref="SetMenuItemDoNotParseCommand"/> through the EF-backed
/// <see cref="IAdminCatalogStore"/>. This is the item-level protection write the Step 9 parser gate
/// observes (invariant #3, admin &gt; parser).
/// </summary>
public sealed partial class SetMenuItemDoNotParseCommandHandler
{
    private readonly IAdminCatalogStore _store;
    private readonly ILogger<SetMenuItemDoNotParseCommandHandler> _logger;

    public SetMenuItemDoNotParseCommandHandler(IAdminCatalogStore store, ILogger<SetMenuItemDoNotParseCommandHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(logger);
        _store = store;
        _logger = logger;
    }

    /// <summary>Sets the flag; returns the store's <see cref="Result"/>.</summary>
    public async Task<Result> HandleAsync(SetMenuItemDoNotParseCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var result = await _store
            .SetMenuItemDoNotParseAsync(command.MenuItemId, command.DoNotParse, cancellationToken)
            .ConfigureAwait(false);

        if (result.IsSuccess)
        {
            LogSet(command.MenuItemId, command.DoNotParse);
        }

        return result;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "Admin set DoNotParse={DoNotParse} on menu item {MenuItemId} (admin > parser).")]
    private partial void LogSet(Guid menuItemId, bool doNotParse);
}
