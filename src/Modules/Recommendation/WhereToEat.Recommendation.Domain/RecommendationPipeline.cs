using WhereToEat.Recommendation.Domain.Filtering;
using WhereToEat.Recommendation.Domain.Matching;
using WhereToEat.Recommendation.Domain.Scoring;
using WhereToEat.Recommendation.Domain.Sorting;

namespace WhereToEat.Recommendation.Domain;

/// <summary>
/// The pure-domain core of the 5-step recommendation pipeline (CLAUDE.md §6). It is handed the
/// already-resolved strategies (match / sort / the selected filters) and the candidates the data
/// layer narrowed by selection, and it runs Steps 2–5 deterministically:
/// <list type="number">
///   <item><b>Step 1 (match)</b> — applies <see cref="IMatchStrategy.Qualify"/>: drops non-qualifying
///   candidates and computes each basket price.</item>
///   <item><b>Step 2 (aggregate)</b> — builds <see cref="AggregatedCandidate"/> (basket price, the
///   smoothed rating carried on the candidate, distance, coverage).</item>
///   <item><b>Step 3 (rank)</b> — defers to the <see cref="ISortStrategy"/> (explicit sort dominates;
///   composite blends — invariant #5).</item>
///   <item><b>Step 4 (filter)</b> — applies the selected <see cref="IResultFilter"/>s, composably.</item>
///   <item><b>Step 5 (deliver)</b> — returns the ordered, filtered list.</item>
/// </list>
/// No I/O — the candidates, the median (inside the context) and the strategies are all supplied. The
/// Application handler resolves the strategies by key and reads the median through its port, then
/// calls this. Filtering is applied <b>after</b> ranking so a filter never changes the order of what
/// survives.
/// </summary>
public sealed class RecommendationPipeline
{
    /// <summary>
    /// Runs the pipeline over <paramref name="candidates"/> with the resolved strategies.
    /// </summary>
    /// <param name="candidates">The candidates the data layer matched by selection.</param>
    /// <param name="selectionCount">How many distinct items the user selected (the match "N").</param>
    /// <param name="match">The resolved match predicate (Step 1).</param>
    /// <param name="sort">The resolved ranking strategy (Step 3).</param>
    /// <param name="filters">
    /// The resolved filters paired with their raw request values (Step 4). May be empty.
    /// </param>
    /// <param name="context">The scoring context (per-mode weights, median, max coverage, distance flag).</param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "The pipeline is an injectable instance seam (resolved from DI by the handler), " +
            "kept non-static so a future pipeline variant can carry policy/state without changing call sites.")]
    public IReadOnlyList<AggregatedCandidate> Run(
        IReadOnlyList<RestaurantCandidate> candidates,
        int selectionCount,
        IMatchStrategy match,
        ISortStrategy sort,
        IReadOnlyList<(IResultFilter Filter, string? Value)> filters,
        ScoringContext context)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(sort);
        ArgumentNullException.ThrowIfNull(filters);
        ArgumentNullException.ThrowIfNull(context);

        // Steps 1 + 2: keep only candidates the match predicate qualifies, and aggregate each into
        // the representative figures (the basket price comes from the predicate; coverage is the
        // matched-item count; the smoothed rating and location ride along from the candidate).
        var aggregated = new List<AggregatedCandidate>(candidates.Count);
        foreach (var candidate in candidates)
        {
            var basketPrice = match.Qualify(candidate, selectionCount);
            if (basketPrice is null)
            {
                continue;
            }

            aggregated.Add(new AggregatedCandidate(
                candidate.RestaurantId,
                candidate.Name,
                basketPrice,
                candidate.SmoothedRating,
                candidate.RatingCount,
                // The exact distance is precomputed by the data layer (Haversine, invariant #7) and
                // rides on the candidate; surface it only when distance ranking is enabled.
                context.DistanceEnabled ? candidate.DistanceKm : null,
                candidate.MatchedItems.Count));
        }

        // Step 3: rank. The sort strategy owns invariant #5 (explicit field dominates / composite blends).
        var ranked = sort.Rank(aggregated, context);

        // Step 4: filter, composably, AFTER ranking (order of survivors is preserved). Every selected
        // filter must keep a candidate for it to survive.
        if (filters.Count == 0)
        {
            return ranked;
        }

        return ranked
            .Where(candidate => filters.All(f => f.Filter.Keep(candidate, f.Value)))
            .ToList();
    }
}
