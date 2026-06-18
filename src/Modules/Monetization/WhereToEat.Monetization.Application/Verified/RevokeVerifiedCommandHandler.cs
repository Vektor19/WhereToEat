using Microsoft.Extensions.Logging;
using WhereToEat.Monetization.Domain.Abstractions;
using WhereToEat.Monetization.Domain.Identifiers;
using WhereToEat.SharedKernel.Results;

namespace WhereToEat.Monetization.Application.Verified;

/// <summary>
/// Revokes a venue's Verified status (back to the freemium baseline). Loads the aggregate, applies the
/// domain <c>Revoke</c> (tier → None, real-photo gate closes), and persists. A venue with no status is
/// already at the baseline, so revoking is a no-op success (idempotent). Organic ranking and the
/// always-free contact links are untouched (invariant #10 / §5.8).
/// </summary>
public sealed partial class RevokeVerifiedCommandHandler
{
    private readonly IVerifiedStatusRepository _repository;
    private readonly ILogger<RevokeVerifiedCommandHandler> _logger;

    public RevokeVerifiedCommandHandler(
        IVerifiedStatusRepository repository,
        ILogger<RevokeVerifiedCommandHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(logger);
        _repository = repository;
        _logger = logger;
    }

    /// <summary>Revokes Verified; a no-op success when the venue has no status (already at the baseline).</summary>
    public async Task<Result> HandleAsync(RevokeVerifiedCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var venue = VenueRef.From(command.VenueId);
        var status = await _repository.GetByVenueAsync(venue, cancellationToken).ConfigureAwait(false);
        if (status is null)
        {
            // Already at the freemium baseline — nothing to revoke.
            LogRevoked(command.VenueId);
            return Result.Success();
        }

        status.Revoke();
        await _repository.UpsertAsync(status, cancellationToken).ConfigureAwait(false);

        LogRevoked(command.VenueId);
        return Result.Success();
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "Revoked Verified for venue {VenueId}; real-photo gate closed, organic ranking untouched.")]
    private partial void LogRevoked(Guid venueId);
}
