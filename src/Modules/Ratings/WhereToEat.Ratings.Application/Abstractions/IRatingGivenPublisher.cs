using WhereToEat.Contracts.IntegrationEvents;

namespace WhereToEat.Ratings.Application.Abstractions;

/// <summary>
/// The seam by which a submitted rating raises the <see cref="RatingGiven"/> integration event so the
/// existing rating-recompute job rebuilds the cumulative all-time aggregate and the smoothed value the
/// recommend/details paths display (invariant #6). The Ratings module never references the recommendation
/// module: it only publishes the <c>Contracts</c> event, and the recompute consumer/job picks it up — so
/// the engine stays decoupled (publish the event, don't call recompute directly). The MassTransit-backed
/// adapter lives in <c>WhereToEat.Ratings.Infrastructure</c>; keeping the port here lets the Application
/// stay MassTransit-free (the dependency-direction rule), mirroring the Admin module's
/// <c>IAddressChangedDispatcher</c> seam.
/// </summary>
public interface IRatingGivenPublisher
{
    /// <summary>
    /// Publishes <paramref name="ratingGiven"/> on the bus. Called only after the rating fact is
    /// persisted, so a publish failure surfaces as the submit's failure rather than silently dropping a
    /// recorded rating — the recompute is downstream and idempotent over the raw facts.
    /// </summary>
    Task PublishAsync(RatingGiven ratingGiven, CancellationToken cancellationToken = default);
}
