using WhereToEat.Ratings.Domain.Identifiers;
using WhereToEat.SharedKernel.Primitives;
using WhereToEat.SharedKernel.Results;

namespace WhereToEat.Ratings.Domain;

/// <summary>
/// The per-restaurant <b>cumulative, all-time</b> rating rollup (invariant #6): the running
/// <see cref="Sum"/> of every raw score the venue ever received and the <see cref="Count"/> of those
/// scores. The single value ranking consumes is the <b>Bayesian additive-prior</b> smoothed average
/// (<see cref="SmoothedValue"/>), computed via the authoritative <see cref="BayesianSmoothing"/> math
/// from <see cref="Sum"/>, <see cref="Count"/>, and the configurable <see cref="RatingSmoothingOptions"/>.
///
/// <para>
/// This is the hot-path read model the recommendation engine joins (as a plain DTO field, never via a
/// code reference into this domain) and that the Step 12 recompute job materializes. Because the
/// smoothing is pure math over the same <see cref="Sum"/>/<see cref="Count"/>, the materialized value
/// always matches the formula. The aggregate is keyed by the opaque <see cref="RestaurantRef"/> so the
/// Ratings module stays independent of the Catalog module (the module-isolation fitness rule).
/// </para>
/// </summary>
public sealed class RatingAggregate : AggregateRoot<RestaurantRef>
{
    private RatingAggregate(RestaurantRef restaurant, double sum, long count)
        : base(restaurant)
    {
        Sum = sum;
        Count = count;
    }

    /// <summary>The all-time sum of every raw score the venue received (non-negative).</summary>
    public double Sum { get; private set; }

    /// <summary>The all-time number of reviews the venue received (non-negative).</summary>
    public long Count { get; private set; }

    /// <summary>The opaque restaurant this rollup belongs to (the aggregate's identity).</summary>
    public RestaurantRef Restaurant => Id;

    /// <summary>
    /// Starts an empty rollup for a restaurant — zero sum, zero count. Its
    /// <see cref="SmoothedValue"/> resolves to the global mean <c>m</c> until reviews arrive.
    /// </summary>
    public static Result<RatingAggregate> Empty(RestaurantRef restaurant)
        => Create(restaurant, sum: 0d, count: 0L);

    /// <summary>
    /// Rehydrates a rollup from its persisted/seeded <paramref name="sum"/> and
    /// <paramref name="count"/> (the Step 12 recompute job and the Dapper read path use this),
    /// rejecting a default restaurant reference, a negative/non-finite sum, or a negative count.
    /// </summary>
    public static Result<RatingAggregate> Create(RestaurantRef restaurant, double sum, long count)
    {
        if (restaurant == default)
        {
            return Result.Failure<RatingAggregate>(
                Error.Validation("RatingAggregate.RestaurantRequired", "A rating aggregate must reference a restaurant."));
        }

        if (count < 0L)
        {
            return Result.Failure<RatingAggregate>(
                Error.Validation("RatingAggregate.NegativeCount", "A rating aggregate's review count cannot be negative."));
        }

        if (sum < 0d || !double.IsFinite(sum))
        {
            return Result.Failure<RatingAggregate>(
                Error.Validation("RatingAggregate.InvalidSum", "A rating aggregate's score sum must be finite and non-negative."));
        }

        return Result.Success(new RatingAggregate(restaurant, sum, count));
    }

    /// <summary>
    /// Folds one new raw score into the cumulative rollup (a user rated the venue), rejecting a
    /// score outside the <see cref="Rating.MinScore"/>..<see cref="Rating.MaxScore"/> scale.
    /// </summary>
    public Result ApplyNewScore(int score)
    {
        if (score < Rating.MinScore || score > Rating.MaxScore)
        {
            return Result.Failure(
                Error.Validation("RatingAggregate.ScoreOutOfRange", $"A rating score must be between {Rating.MinScore} and {Rating.MaxScore}."));
        }

        Sum += score;
        Count += 1L;
        return Result.Success();
    }

    /// <summary>
    /// Folds a user's score <i>revision</i> into the rollup: the count is unchanged (the same review)
    /// and the sum is adjusted by the delta. Rejects either score being out of range.
    /// </summary>
    public Result ApplyScoreRevision(int previousScore, int newScore)
    {
        if (previousScore < Rating.MinScore || previousScore > Rating.MaxScore
            || newScore < Rating.MinScore || newScore > Rating.MaxScore)
        {
            return Result.Failure(
                Error.Validation("RatingAggregate.ScoreOutOfRange", $"A rating score must be between {Rating.MinScore} and {Rating.MaxScore}."));
        }

        if (Count == 0L)
        {
            return Result.Failure(
                Error.Conflict("RatingAggregate.NoReviewToRevise", "Cannot revise a score on an aggregate with no reviews."));
        }

        Sum += newScore - previousScore;
        return Result.Success();
    }

    /// <summary>
    /// The Bayesian additive-prior smoothed value <c>(C*m + Sum) / (C + Count)</c> the ranking
    /// consumes — computed from this rollup's <see cref="Sum"/>/<see cref="Count"/> and the supplied
    /// <paramref name="options"/> via the single authoritative <see cref="BayesianSmoothing"/> formula.
    /// </summary>
    public double SmoothedValue(RatingSmoothingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return BayesianSmoothing.Smooth(Sum, Count, options);
    }
}
