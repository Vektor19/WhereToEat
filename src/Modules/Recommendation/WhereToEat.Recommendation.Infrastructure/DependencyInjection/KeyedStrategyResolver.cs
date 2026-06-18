using WhereToEat.Recommendation.Application.Abstractions;
using WhereToEat.Recommendation.Domain;
using WhereToEat.Recommendation.Domain.Filtering;
using WhereToEat.Recommendation.Domain.Matching;
using WhereToEat.Recommendation.Domain.Sorting;

namespace WhereToEat.Recommendation.Infrastructure.DependencyInjection;

/// <summary>
/// The <see cref="IStrategyResolver"/> backed by simple key→strategy maps built from the registered
/// strategies (invariant #4 — pluggable by key). Because the maps are keyed by each strategy's own
/// <c>Key</c>, registering a <b>new</b> match/sort/filter (added to the collections in DI) makes it
/// resolvable <b>without</b> changing this resolver, the pipeline, or the handler — exactly the
/// "self-registers under a new key" property the pluggability test pins. Unknown keys return
/// <c>null</c> so the handler can produce a clean validation failure.
/// </summary>
public sealed class KeyedStrategyResolver : IStrategyResolver
{
    private readonly Dictionary<string, IMatchStrategy> _matchByKey;
    private readonly Dictionary<string, ISortStrategy> _sortByKey;
    private readonly Dictionary<string, IResultFilter> _filterByKey;
    private readonly Dictionary<string, RecommendationModeOptions> _optionsByKey;
    private readonly RecommendationModeOptions _defaultOptions;

    /// <summary>
    /// Builds the resolver from all registered strategies. Each collection is indexed by the
    /// strategy's own key; a duplicate key throws (a registration bug), so the maps are unambiguous.
    /// </summary>
    public KeyedStrategyResolver(
        IEnumerable<IMatchStrategy> matchStrategies,
        IEnumerable<ISortStrategy> sortStrategies,
        IEnumerable<IResultFilter> filters,
        IReadOnlyDictionary<string, RecommendationModeOptions> optionsByKey,
        RecommendationModeOptions defaultOptions)
    {
        ArgumentNullException.ThrowIfNull(matchStrategies);
        ArgumentNullException.ThrowIfNull(sortStrategies);
        ArgumentNullException.ThrowIfNull(filters);
        ArgumentNullException.ThrowIfNull(optionsByKey);
        ArgumentNullException.ThrowIfNull(defaultOptions);

        _matchByKey = matchStrategies.ToDictionary(s => s.Key, StringComparer.OrdinalIgnoreCase);
        _sortByKey = sortStrategies.ToDictionary(s => s.Key, StringComparer.OrdinalIgnoreCase);
        _filterByKey = filters.ToDictionary(f => f.Key, StringComparer.OrdinalIgnoreCase);
        _optionsByKey = new Dictionary<string, RecommendationModeOptions>(optionsByKey, StringComparer.OrdinalIgnoreCase);
        _defaultOptions = defaultOptions;
    }

    /// <inheritdoc />
    public IMatchStrategy? ResolveMatch(string matchKey)
        => matchKey is not null && _matchByKey.TryGetValue(matchKey, out var s) ? s : null;

    /// <inheritdoc />
    public ISortStrategy? ResolveSort(string sortKey)
        => sortKey is not null && _sortByKey.TryGetValue(sortKey, out var s) ? s : null;

    /// <inheritdoc />
    public IResultFilter? ResolveFilter(string filterKey)
        => filterKey is not null && _filterByKey.TryGetValue(filterKey, out var f) ? f : null;

    /// <inheritdoc />
    public RecommendationModeOptions OptionsFor(string sortKey)
        => sortKey is not null && _optionsByKey.TryGetValue(sortKey, out var o) ? o : _defaultOptions;
}
