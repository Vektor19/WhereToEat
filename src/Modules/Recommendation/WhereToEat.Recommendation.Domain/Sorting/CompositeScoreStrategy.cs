using WhereToEat.Recommendation.Domain.Scoring;

namespace WhereToEat.Recommendation.Domain.Sorting;

/// <summary>
/// The <b>composite-score</b> ranking (CLAUDE.md §6 Step 3 (B)): for modes that are an inherent
/// compromise (<c>price-quality</c>, <c>best</c>) there is no single obvious field, so it computes a
/// weighted sum of the normalized scoring functions
/// <c>score = Σ weight_f · f(candidate)</c> over the registered <see cref="IScoringFunction"/>s and
/// sorts by it (descending). The per-mode weights come from <see cref="ScoringContext.Options"/>, so
/// the same strategy class realizes every composite mode by being constructed with a different
/// weight map — changing a weight changes the ordering (the weight-sensitivity the tests pin).
/// <para>
/// Pluggable: the scoring functions are injected, so adding a new <c>f_*</c> (with a weight key) adds
/// a facet without editing this strategy; restaurant id is the final tie-breaker for stability.
/// </para>
/// </summary>
public sealed class CompositeScoreStrategy : ISortStrategy
{
    /// <summary>Sort key for the balanced price-and-quality composite mode.</summary>
    public const string PriceQualityKey = "price-quality";

    /// <summary>Sort key for the all-round "best" composite mode.</summary>
    public const string BestKey = "best";

    private readonly IReadOnlyList<IScoringFunction> _functions;
    private readonly IReadOnlyDictionary<string, double> _weights;

    /// <summary>
    /// Creates a composite strategy under <paramref name="key"/> blending
    /// <paramref name="functions"/> with the per-factor <paramref name="weights"/> (keyed by each
    /// function's <see cref="IScoringFunction.Key"/>; a function with no weight contributes 0).
    /// </summary>
    public CompositeScoreStrategy(
        string key,
        IReadOnlyList<IScoringFunction> functions,
        IReadOnlyDictionary<string, double> weights)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(functions);
        ArgumentNullException.ThrowIfNull(weights);

        Key = key;
        _functions = functions;
        _weights = weights;
    }

    /// <inheritdoc />
    public string Key { get; }

    /// <summary>
    /// Computes the composite score of one candidate (exposed so tests and callers can inspect the
    /// blended value, not only the ordering). Each function is clamped to 0…1 internally.
    /// </summary>
    public double ScoreOf(AggregatedCandidate candidate, ScoringContext context)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(context);

        var total = 0d;
        foreach (var function in _functions)
        {
            var weight = _weights.TryGetValue(function.Key, out var w) ? w : 0d;
            if (weight == 0d)
            {
                continue;
            }

            total += weight * function.Score(candidate, context);
        }

        return total;
    }

    /// <inheritdoc />
    public IReadOnlyList<AggregatedCandidate> Rank(
        IReadOnlyList<AggregatedCandidate> candidates,
        ScoringContext context)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(context);

        return candidates
            .OrderByDescending(c => ScoreOf(c, context))
            .ThenBy(c => c.RestaurantId)
            .ToList();
    }
}
