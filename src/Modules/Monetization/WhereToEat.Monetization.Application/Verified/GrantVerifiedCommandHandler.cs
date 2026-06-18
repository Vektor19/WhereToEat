using Microsoft.Extensions.Logging;
using WhereToEat.Monetization.Application.Payments;
using WhereToEat.Monetization.Domain;
using WhereToEat.Monetization.Domain.Abstractions;
using WhereToEat.Monetization.Domain.Identifiers;
using WhereToEat.SharedKernel.Results;

namespace WhereToEat.Monetization.Application.Verified;

/// <summary>
/// Grants/upgrades a venue's Verified status: authorize payment through the <see cref="IPaymentGateway"/>
/// seam (the SOLE payment touch-point), then load-or-create the <see cref="VerifiedStatus"/> aggregate,
/// apply the domain <see cref="VerifiedStatus.Grant"/> (which rejects a non-paid tier), and persist.
/// The effect is exactly: the real-photo permission gate opens (Step 5) and the tier is set — organic
/// ranking and the always-free contact links are untouched (invariant #10 / §5.8).
/// </summary>
public sealed partial class GrantVerifiedCommandHandler
{
    private readonly IVerifiedStatusRepository _repository;
    private readonly IPaymentGateway _paymentGateway;
    private readonly ILogger<GrantVerifiedCommandHandler> _logger;

    public GrantVerifiedCommandHandler(
        IVerifiedStatusRepository repository,
        IPaymentGateway paymentGateway,
        ILogger<GrantVerifiedCommandHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(paymentGateway);
        ArgumentNullException.ThrowIfNull(logger);
        _repository = repository;
        _paymentGateway = paymentGateway;
        _logger = logger;
    }

    /// <summary>Grants the requested paid tier; returns a failure if payment or the domain guard rejects it.</summary>
    public async Task<Result> HandleAsync(GrantVerifiedCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.Tier == SubscriptionTier.None)
        {
            return Result.Failure(Error.Validation(
                "GrantVerified.RequiresPaidTier", "Granting Verified requires a paid tier (Basic or Pro)."));
        }

        var venue = VenueRef.From(command.VenueId);

        // Payment runs through the seam ONLY (no provider call anywhere else). A failed authorization
        // means the feature is not granted.
        var authorization = await _paymentGateway
            .AuthorizeAsync(venue, $"Verified:{command.Tier}", cancellationToken)
            .ConfigureAwait(false);
        if (authorization.IsFailure)
        {
            return Result.Failure(authorization.Error);
        }

        var status = await _repository.GetByVenueAsync(venue, cancellationToken).ConfigureAwait(false);
        if (status is null)
        {
            var created = VerifiedStatus.Unverified(venue);
            if (created.IsFailure)
            {
                return Result.Failure(created.Error);
            }

            status = created.Value;
        }

        var grant = status.Grant(command.Tier);
        if (grant.IsFailure)
        {
            return grant;
        }

        await _repository.UpsertAsync(status, cancellationToken).ConfigureAwait(false);

        LogGranted(command.VenueId, command.Tier, authorization.Value.AuthorizationId);
        return Result.Success();
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "Granted Verified tier {Tier} to venue {VenueId} (payment auth {AuthorizationId}); " +
            "real-photo gate open, organic ranking untouched (invariant #10).")]
    private partial void LogGranted(Guid venueId, SubscriptionTier tier, string authorizationId);
}
