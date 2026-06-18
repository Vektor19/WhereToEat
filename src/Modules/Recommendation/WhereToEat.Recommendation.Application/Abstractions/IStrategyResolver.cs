using WhereToEat.Recommendation.Domain.Filtering;
using WhereToEat.Recommendation.Domain.Matching;
using WhereToEat.Recommendation.Domain.Sorting;

namespace WhereToEat.Recommendation.Application.Abstractions;

/// <summary>
/// Resolves the pipeline's pluggable strategies <b>by the request key</b> (invariant #4). The
/// handler asks for the match/sort strategy named in the request and the filters named in the
/// request's filter list; the registration (in Infrastructure DI) maps each key to its strategy, so
/// a new match/sort/filter self-registers under a new key <b>without</b> the handler or the pipeline
/// changing. Unknown keys return <c>null</c> so the handler can answer with a clean validation
/// failure rather than throwing.
/// </summary>
public interface IStrategyResolver
{
    /// <summary>Resolves the match predicate for <paramref name="matchKey"/>, or <c>null</c> if unknown.</summary>
    IMatchStrategy? ResolveMatch(string matchKey);

    /// <summary>Resolves the ranking strategy for <paramref name="sortKey"/>, or <c>null</c> if unknown.</summary>
    ISortStrategy? ResolveSort(string sortKey);

    /// <summary>Resolves the result filter for <paramref name="filterKey"/>, or <c>null</c> if unknown.</summary>
    IResultFilter? ResolveFilter(string filterKey);

    /// <summary>
    /// The per-mode scoring options for <paramref name="sortKey"/>. Explicit-sort modes still return
    /// options (used to normalize the figures echoed back); composite modes return their tuned weights.
    /// </summary>
    Domain.RecommendationModeOptions OptionsFor(string sortKey);
}
