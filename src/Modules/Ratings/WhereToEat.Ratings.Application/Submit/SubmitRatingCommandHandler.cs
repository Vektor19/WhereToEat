using Microsoft.Extensions.Logging;
using WhereToEat.Contracts.IntegrationEvents;
using WhereToEat.Ratings.Application.Abstractions;
using WhereToEat.Ratings.Domain;
using WhereToEat.Ratings.Domain.Abstractions;
using WhereToEat.Ratings.Domain.Identifiers;
using WhereToEat.SharedKernel.Results;

namespace WhereToEat.Ratings.Application.Submit;

/// <summary>
/// Applies a <see cref="SubmitRatingCommand"/>: looks up any existing rating the user already left for
/// the restaurant (the <see cref="UQ_Rating_Restaurant_User"/>-backed one-per-user-per-restaurant
/// invariant), then either <b>revises</b> that fact in place (Domain <see cref="Rating.Revise"/>) or
/// <b>creates</b> a new one (Domain <see cref="Rating.Create"/>), persists it through the
/// <see cref="IRatingRepository"/> write port, and — only on a successful persist — publishes the
/// <see cref="RatingGiven"/> integration event through <see cref="IRatingGivenPublisher"/> so the
/// existing recompute job rebuilds the cumulative all-time aggregate and the smoothed value the
/// recommend/details paths display (invariant #6). The score is validated by the domain (1..5), so an
/// out-of-range value returns the domain's <see cref="Result"/> failure before anything is persisted.
/// </summary>
public sealed partial class SubmitRatingCommandHandler
{
    private readonly IRatingRepository _ratings;
    private readonly IRatingGivenPublisher _publisher;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SubmitRatingCommandHandler> _logger;

    public SubmitRatingCommandHandler(
        IRatingRepository ratings,
        IRatingGivenPublisher publisher,
        TimeProvider timeProvider,
        ILogger<SubmitRatingCommandHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(ratings);
        ArgumentNullException.ThrowIfNull(publisher);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        _ratings = ratings;
        _publisher = publisher;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Submits the rating: revise-or-create, persist, then publish <see cref="RatingGiven"/>. Returns the
    /// domain's <see cref="Result"/> — an invalid score (or a default ref) fails fast without persisting
    /// or publishing.
    /// </summary>
    public async Task<Result> HandleAsync(SubmitRatingCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var restaurant = RestaurantRef.From(command.RestaurantId);
        var user = UserRef.From(command.UserId);
        var now = _timeProvider.GetUtcNow();

        var existing = await _ratings
            .GetByRestaurantAndUserAsync(restaurant, user, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            // First time this user rates this venue: create the fact (the domain validates the score).
            var created = Rating.Create(restaurant, user, command.Score, now);
            if (created.IsFailure)
            {
                return created;
            }

            await _ratings.AddAsync(created.Value, cancellationToken).ConfigureAwait(false);
            LogRatingCreated(command.RestaurantId);
        }
        else
        {
            // The user is revising their existing score: same row, never a duplicate (the unique
            // constraint backs this, and the domain re-validates the new score).
            var revised = existing.Revise(command.Score, now);
            if (revised.IsFailure)
            {
                return revised;
            }

            await _ratings.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);
            LogRatingRevised(command.RestaurantId);
        }

        // The fact is persisted; raise RatingGiven so the recompute job rebuilds the smoothed aggregate
        // (invariant #6). We publish AFTER a successful persist so we never signal a rating that did not
        // land, and we don't call the recompute directly — the engine stays decoupled (publish only).
        var @event = new RatingGiven(
            EventId: Guid.NewGuid(),
            OccurredOnUtc: now,
            RestaurantId: command.RestaurantId,
            Score: command.Score);

        await _publisher.PublishAsync(@event, cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "User submitted a new rating for restaurant {RestaurantId}; publishing RatingGiven → recompute.")]
    private partial void LogRatingCreated(Guid restaurantId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information,
        Message = "User revised their rating for restaurant {RestaurantId}; publishing RatingGiven → recompute.")]
    private partial void LogRatingRevised(Guid restaurantId);
}
