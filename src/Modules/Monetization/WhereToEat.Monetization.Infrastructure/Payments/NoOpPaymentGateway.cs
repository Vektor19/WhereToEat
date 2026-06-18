using Microsoft.Extensions.Logging;
using WhereToEat.Monetization.Application.Payments;
using WhereToEat.Monetization.Domain.Identifiers;
using WhereToEat.SharedKernel.Results;

namespace WhereToEat.Monetization.Infrastructure.Payments;

/// <summary>
/// The <b>dev/no-op</b> implementation of the <see cref="IPaymentGateway"/> seam — the ONLY payment
/// implementation in the system (Step 14; real billing is a non-goal). It authorizes every charge
/// <b>without contacting any provider</b>, minting a synthetic authorization id, so the grant-Verified
/// / create-ad use-cases run end-to-end with no external dependency. A future real provider (Stripe,
/// LiqPay, …) is introduced by registering a different <see cref="IPaymentGateway"/> here — the
/// use-cases and the domain never change, because this seam is the single payment touch-point.
/// </summary>
public sealed partial class NoOpPaymentGateway : IPaymentGateway
{
    private readonly ILogger<NoOpPaymentGateway> _logger;

    public NoOpPaymentGateway(ILogger<NoOpPaymentGateway> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<Result<PaymentAuthorization>> AuthorizeAsync(
        VenueRef venue,
        string reference,
        CancellationToken cancellationToken = default)
    {
        // No provider is contacted; a synthetic authorization id is minted so the use-case has a
        // reference to record. This is intentionally always-success — the seam exists so a real
        // provider can later reject; the dev gateway never does.
        var authorizationId = $"noop-{Guid.NewGuid():N}";
        LogAuthorized(venue.Value, reference, authorizationId);
        return Task.FromResult(Result.Success(new PaymentAuthorization(authorizationId)));
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "No-op payment authorization for venue {VenueId} ({Reference}) → {AuthorizationId} " +
            "(dev gateway; no provider contacted — real billing is a non-goal).")]
    private partial void LogAuthorized(Guid venueId, string reference, string authorizationId);
}
