using WhereToEat.Monetization.Domain;

namespace WhereToEat.Monetization.Application.Verified;

/// <summary>
/// Grants (or upgrades) a venue's Verified status to a paid <paramref name="Tier"/> (Basic/Pro). This
/// unlocks the Step 5 real-photo permission gate and sets the tier; it has <b>no</b> effect on organic
/// ranking (invariant #10) and does <b>not</b> touch the always-free contact links (§5.8). Payment is
/// authorized strictly through the <c>IPaymentGateway</c> seam by the handler.
/// </summary>
/// <param name="VenueId">The venue (its restaurant Guid) being granted Verified.</param>
/// <param name="Tier">The paid tier to grant — must be Basic or Pro, never <see cref="SubscriptionTier.None"/>.</param>
public sealed record GrantVerifiedCommand(Guid VenueId, SubscriptionTier Tier);
