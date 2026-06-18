using WhereToEat.Ratings.Domain.Identifiers;
using WhereToEat.SharedKernel.Primitives;
using WhereToEat.SharedKernel.Results;

namespace WhereToEat.Ratings.Domain;

/// <summary>
/// A single registered user's score for a restaurant (invariant #6 — our own ratings). Scores are
/// integers on the <see cref="MinScore"/>..<see cref="MaxScore"/> scale. A rating carries only the
/// opaque <see cref="UserRef"/> and <see cref="RestaurantRef"/> (Guids) plus the score and the time
/// it was given — never any PII (invariant #11). The per-restaurant rollup that ranking consumes is
/// the <see cref="RatingAggregate"/>; this is the raw fact the aggregate is recomputed from.
/// </summary>
public sealed class Rating : AggregateRoot<RatingId>
{
    /// <summary>The inclusive minimum score a user may give.</summary>
    public const int MinScore = 1;

    /// <summary>The inclusive maximum score a user may give.</summary>
    public const int MaxScore = 5;

    private Rating(RatingId id, RestaurantRef restaurant, UserRef user, int score, DateTimeOffset givenAt)
        : base(id)
    {
        Restaurant = restaurant;
        User = user;
        Score = score;
        GivenAt = givenAt;
    }

    /// <summary>The restaurant this score is about.</summary>
    public RestaurantRef Restaurant { get; }

    /// <summary>The user who gave the score (opaque reference; no PII).</summary>
    public UserRef User { get; }

    /// <summary>The score on the <see cref="MinScore"/>..<see cref="MaxScore"/> scale.</summary>
    public int Score { get; private set; }

    /// <summary>When the score was given (used only as an audit timestamp, not for ranking).</summary>
    public DateTimeOffset GivenAt { get; private set; }

    /// <summary>Records a new rating with a fresh id; see <see cref="Create(RatingId, RestaurantRef, UserRef, int, DateTimeOffset)"/>.</summary>
    public static Result<Rating> Create(RestaurantRef restaurant, UserRef user, int score, DateTimeOffset givenAt)
        => Create(RatingId.New(), restaurant, user, score, givenAt);

    /// <summary>
    /// Records a rating (with an explicit id for rehydration/seeding), rejecting a score outside the
    /// <see cref="MinScore"/>..<see cref="MaxScore"/> scale and a default restaurant/user reference.
    /// </summary>
    public static Result<Rating> Create(RatingId id, RestaurantRef restaurant, UserRef user, int score, DateTimeOffset givenAt)
    {
        if (restaurant == default)
        {
            return Result.Failure<Rating>(
                Error.Validation("Rating.RestaurantRequired", "A rating must reference a restaurant."));
        }

        if (user == default)
        {
            return Result.Failure<Rating>(
                Error.Validation("Rating.UserRequired", "A rating must reference a user."));
        }

        if (score < MinScore || score > MaxScore)
        {
            return Result.Failure<Rating>(
                Error.Validation("Rating.ScoreOutOfRange", $"A rating score must be between {MinScore} and {MaxScore}."));
        }

        return Result.Success(new Rating(id, restaurant, user, score, givenAt));
    }

    /// <summary>
    /// Updates the score (a user revising their own rating), rejecting an out-of-range value and
    /// re-stamping <see cref="GivenAt"/>.
    /// </summary>
    public Result Revise(int score, DateTimeOffset revisedAt)
    {
        if (score < MinScore || score > MaxScore)
        {
            return Result.Failure(
                Error.Validation("Rating.ScoreOutOfRange", $"A rating score must be between {MinScore} and {MaxScore}."));
        }

        Score = score;
        GivenAt = revisedAt;
        return Result.Success();
    }
}
