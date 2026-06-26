using MassTransit;
using WhereToEat.Contracts.IntegrationEvents;
using WhereToEat.Ratings.Application.Abstractions;

namespace WhereToEat.Ratings.Infrastructure.Messaging;

/// <summary>
/// The MassTransit-backed <see cref="IRatingGivenPublisher"/>: publishes the <see cref="RatingGiven"/>
/// integration event onto the bus (in-process now, RabbitMQ by config — the same publish/consume
/// semantics either way, invariant #4) so the existing rating-recompute job rebuilds the cumulative
/// all-time aggregate and the smoothed value the recommend/details paths display (invariant #6). The
/// MassTransit dependency is confined to this adapter so the Ratings.Application use-case stays
/// transport-agnostic (the dependency-direction rule), mirroring how the admin host's address-changed
/// dispatcher confines its transport concern.
/// </summary>
public sealed class MassTransitRatingGivenPublisher : IRatingGivenPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;

    public MassTransitRatingGivenPublisher(IPublishEndpoint publishEndpoint)
    {
        ArgumentNullException.ThrowIfNull(publishEndpoint);
        _publishEndpoint = publishEndpoint;
    }

    /// <inheritdoc />
    public Task PublishAsync(RatingGiven ratingGiven, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ratingGiven);
        return _publishEndpoint.Publish(ratingGiven, cancellationToken);
    }
}
