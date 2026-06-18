using Microsoft.Extensions.Logging;
using WhereToEat.Admin.Application.Abstractions;
using WhereToEat.SharedKernel.Results;

namespace WhereToEat.Admin.Application.Photos;

/// <summary>
/// Applies a <see cref="ManageRealPhotoCommand"/> through the EF-backed <see cref="IAdminCatalogStore"/>,
/// flipping a real photo's permission gate (invariant #8). The store rejects toggling a generic photo
/// (our own content needs no permission).
/// </summary>
public sealed partial class ManageRealPhotoCommandHandler
{
    private readonly IAdminCatalogStore _store;
    private readonly ILogger<ManageRealPhotoCommandHandler> _logger;

    public ManageRealPhotoCommandHandler(IAdminCatalogStore store, ILogger<ManageRealPhotoCommandHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(logger);
        _store = store;
        _logger = logger;
    }

    /// <summary>Sets the real-photo permission; returns the store's <see cref="Result"/>.</summary>
    public async Task<Result> HandleAsync(ManageRealPhotoCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var result = await _store
            .SetRealPhotoPermissionAsync(command.PhotoId, command.PermissionGranted, cancellationToken)
            .ConfigureAwait(false);

        if (result.IsSuccess)
        {
            LogSet(command.PhotoId, command.PermissionGranted);
        }

        return result;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "Admin set PermissionGranted={PermissionGranted} on real photo {PhotoId} (invariant #8 gate).")]
    private partial void LogSet(Guid photoId, bool permissionGranted);
}
