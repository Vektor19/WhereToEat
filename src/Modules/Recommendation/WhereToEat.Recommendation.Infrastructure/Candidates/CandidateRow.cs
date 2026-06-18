namespace WhereToEat.Recommendation.Infrastructure.Candidates;

/// <summary>
/// A flat row returned by the candidate query (<see cref="Medians.RecommendationSql.SelectCandidates"/>):
/// one matched (restaurant, selection) pairing with the venue's coordinates and the materialized
/// rating-aggregate inputs (<see cref="ScoreSum"/>/<see cref="ScoreCount"/>) joined from the ratings
/// rollup. The smoothed rating is derived in code from these two numbers — the join carries the
/// inputs, never a <c>Ratings.Domain</c> reference. Multiple rows per restaurant are grouped into
/// one candidate by the source.
/// </summary>
internal sealed class CandidateRow
{
    public Guid RestaurantId { get; init; }
    public string Name { get; init; } = string.Empty;
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }

    // The materialized Bayesian-smoothing inputs (NULL when the venue has no rating aggregate yet).
    public double? ScoreSum { get; init; }
    public long? ScoreCount { get; init; }

    // Exactly one of these identifies which selection this row matched (the two-level taxonomy).
    public Guid? CategoryId { get; init; }
    public Guid? DishId { get; init; }

    public decimal PriceAmount { get; init; }
    public string PriceCurrency { get; init; } = string.Empty;
}
