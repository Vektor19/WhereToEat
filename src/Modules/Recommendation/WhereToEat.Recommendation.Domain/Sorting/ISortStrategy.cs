using WhereToEat.Recommendation.Domain.Scoring;

namespace WhereToEat.Recommendation.Domain.Sorting;

/// <summary>
/// Step 3 of the pipeline (CLAUDE.md §6): the <b>ranking</b> strategy — a pluggable module resolved
/// by the request's <c>sort</c> key (invariant #4). Two distinct kinds exist (invariant #5):
/// <list type="bullet">
///   <item><b>Explicit sort</b> (<c>price</c>/<c>distance</c>/<c>rating</c>): the chosen field is the
///   <b>primary</b> key; coverage is <b>only a tie-breaker</b> between equal primary values — the
///   cheapest/nearest/highest-rated venue wins even at 1-of-3.</item>
///   <item><b>Composite</b> (<c>price-quality</c>/<c>best</c>): sort by the weighted normalized
///   <c>f_*</c> score.</item>
/// </list>
/// New sort modes self-register under new keys without touching the pipeline or other strategies.
/// </summary>
public interface ISortStrategy
{
    /// <summary>The request/DI key this strategy answers to (e.g. <c>"price"</c>, <c>"best"</c>).</summary>
    string Key { get; }

    /// <summary>
    /// Returns <paramref name="candidates"/> in ranked order (best first) for
    /// <paramref name="context"/>. The input order is not significant; the strategy produces a
    /// fully deterministic ordering (it breaks any remaining ties by restaurant id for stability).
    /// </summary>
    IReadOnlyList<AggregatedCandidate> Rank(
        IReadOnlyList<AggregatedCandidate> candidates,
        ScoringContext context);
}
