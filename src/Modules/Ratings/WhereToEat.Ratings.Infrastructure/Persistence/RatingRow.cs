namespace WhereToEat.Ratings.Infrastructure.Persistence;

/// <summary>
/// The flat row shape Dapper materializes from <c>ratings.Rating</c> before
/// <see cref="DapperRatingRepository"/> rehydrates it into the domain <see cref="WhereToEat.Ratings.Domain.Rating"/>.
/// Kept internal — it is a persistence detail, not part of the module's public surface.
/// </summary>
internal sealed class RatingRow
{
    public Guid Id { get; init; }

    public Guid RestaurantId { get; init; }

    public Guid UserId { get; init; }

    public int Score { get; init; }

    public DateTimeOffset GivenAt { get; init; }
}
