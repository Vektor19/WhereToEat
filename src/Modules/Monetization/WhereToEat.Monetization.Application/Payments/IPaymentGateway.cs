using WhereToEat.Monetization.Domain.Identifiers;
using WhereToEat.SharedKernel.Results;

namespace WhereToEat.Monetization.Application.Payments;

/// <summary>
/// The <b>single</b> payment touch-point of the whole system (Step 14). Every monetization use-case
/// that involves money (granting a paid Verified tier, charging for an ad placement) goes through this
/// seam and <b>nowhere else</b> — so a future real provider (Stripe, LiqPay, …) plugs in here without
/// touching the use-cases or the domain. There is no real billing in this step (a non-goal): the only
/// implementation is the dev <c>NoOpPaymentGateway</c> in <c>Monetization.Infrastructure</c>, which
/// authorizes everything without contacting any provider.
/// </summary>
public interface IPaymentGateway
{
    /// <summary>
    /// Authorizes a charge for a venue's paid feature before it is granted. Returns the authorization
    /// outcome; a failure means the feature must not be granted. The no-op dev implementation always
    /// succeeds (no provider is contacted).
    /// </summary>
    /// <param name="venue">The venue being charged.</param>
    /// <param name="reference">A human-readable description of what is being charged for (e.g. "Verified:Pro").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Result<PaymentAuthorization>> AuthorizeAsync(
        VenueRef venue,
        string reference,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The result of a successful <see cref="IPaymentGateway.AuthorizeAsync"/>: an opaque authorization id
/// the use-case can record. The no-op dev gateway mints a synthetic one; a real provider would return
/// its own. Carries no card/PII data — only the authorization reference.
/// </summary>
/// <param name="AuthorizationId">The provider's (or, in dev, a synthetic) authorization reference.</param>
public readonly record struct PaymentAuthorization(string AuthorizationId);
