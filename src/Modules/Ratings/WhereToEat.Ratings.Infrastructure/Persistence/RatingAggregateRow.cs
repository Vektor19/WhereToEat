namespace WhereToEat.Ratings.Infrastructure.Persistence;

/// <summary>
/// The flat row shape Dapper materializes from <c>ratings.RatingAggregate</c> before
/// <see cref="DapperRatingAggregateRepository"/> rehydrates it into the domain aggregate. Kept
/// internal — it is a persistence detail, not part of the module's public surface.
/// </summary>
internal sealed class RatingAggregateRow
{
    public Guid RestaurantId { get; init; }

    public double ScoreSum { get; init; }

    public long ScoreCount { get; init; }
}
