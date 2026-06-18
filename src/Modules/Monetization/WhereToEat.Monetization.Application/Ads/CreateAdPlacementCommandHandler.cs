using Microsoft.Extensions.Logging;
using WhereToEat.Monetization.Application.Payments;
using WhereToEat.Monetization.Domain;
using WhereToEat.Monetization.Domain.Abstractions;
using WhereToEat.Monetization.Domain.Identifiers;
using WhereToEat.SharedKernel.Results;

namespace WhereToEat.Monetization.Application.Ads;

/// <summary>
/// Creates a labeled ad placement: authorize payment through the <see cref="IPaymentGateway"/> seam
/// (the SOLE payment touch-point), build the <see cref="AdPlacement"/> via its domain factory (which is
/// always-labeled by construction and rejects an invalid window/blank targeting key), and persist.
/// There is deliberately no code path here that could make the slot influence the Step 7 organic score
/// — the Recommendation module does not reference this module at all (a guard test pins that).
/// </summary>
public sealed partial class CreateAdPlacementCommandHandler
{
    private readonly IAdPlacementRepository _repository;
    private readonly IPaymentGateway _paymentGateway;
    private readonly ILogger<CreateAdPlacementCommandHandler> _logger;

    public CreateAdPlacementCommandHandler(
        IAdPlacementRepository repository,
        IPaymentGateway paymentGateway,
        ILogger<CreateAdPlacementCommandHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(paymentGateway);
        ArgumentNullException.ThrowIfNull(logger);
        _repository = repository;
        _paymentGateway = paymentGateway;
        _logger = logger;
    }

    /// <summary>Creates the labeled placement; returns the new id, or a failure if payment/validation rejects it.</summary>
    public async Task<Result<Guid>> HandleAsync(CreateAdPlacementCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var venue = VenueRef.From(command.VenueId);

        // Payment runs through the seam ONLY (no provider call anywhere else).
        var authorization = await _paymentGateway
            .AuthorizeAsync(venue, $"AdPlacement:{command.TargetingKey}", cancellationToken)
            .ConfigureAwait(false);
        if (authorization.IsFailure)
        {
            return Result.Failure<Guid>(authorization.Error);
        }

        var placement = AdPlacement.Create(venue, command.TargetingKey, command.StartsAt, command.EndsAt);
        if (placement.IsFailure)
        {
            return Result.Failure<Guid>(placement.Error);
        }

        await _repository.AddAsync(placement.Value, cancellationToken).ConfigureAwait(false);

        LogCreated(placement.Value.Id.Value, command.VenueId, command.TargetingKey);
        return Result.Success(placement.Value.Id.Value);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "Created labeled ad placement {PlacementId} for venue {VenueId} targeting '{TargetingKey}' " +
            "(a separate marked slot; never an organic-ranking input — invariant #10).")]
    private partial void LogCreated(Guid placementId, Guid venueId, string targetingKey);
}
