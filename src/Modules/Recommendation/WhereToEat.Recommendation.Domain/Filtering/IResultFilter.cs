namespace WhereToEat.Recommendation.Domain.Filtering;

/// <summary>
/// Step 4 of the pipeline (CLAUDE.md §6): a <b>composable result filter</b> applied over the ranked
/// list. Each filter is an independent, pluggable module keyed by its request key (invariant #4);
/// the pipeline applies every filter the request selected, in any order, so adding open-now / vegan
/// / delivery later is just a new <see cref="IResultFilter"/> registered under a new key — no change
/// to the pipeline or the existing filters.
/// </summary>
public interface IResultFilter
{
    /// <summary>The request/DI key this filter answers to (e.g. <c>"price"</c>, <c>"rating"</c>).</summary>
    string Key { get; }

    /// <summary>
    /// Returns <c>true</c> to keep <paramref name="candidate"/>. <paramref name="value"/> is the raw
    /// filter value from the request (the filter parses it); a malformed value keeps the candidate
    /// (a filter never silently drops everything on bad input — it no-ops).
    /// </summary>
    bool Keep(AggregatedCandidate candidate, string? value);
}
