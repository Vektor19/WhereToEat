namespace WhereToEat.Recommendation.Domain.Scoring;

/// <summary>
/// A single normalized scoring function <c>f_*</c> (CLAUDE.md §6 Step 3 (B)): maps one facet of an
/// <see cref="AggregatedCandidate"/> into <b>0…1</b> (higher is better). The composite-score sort
/// combines the registered functions with the per-mode weights; each function is an independent,
/// pluggable module (invariant #4) so a new facet adds without changing the others. Pure math — no
/// I/O; any external input (e.g. the price median) is supplied through <see cref="ScoringContext"/>.
/// </summary>
public interface IScoringFunction
{
    /// <summary>
    /// The factor key this function scores (e.g. <c>"price"</c>, <c>"quality"</c>, <c>"distance"</c>,
    /// <c>"coverage"</c>). The composite strategy looks up the per-key weight by this.
    /// </summary>
    string Key { get; }

    /// <summary>
    /// Returns the normalized 0…1 score for <paramref name="candidate"/> in
    /// <paramref name="context"/>. Implementations clamp to [0, 1] and never throw on a missing
    /// optional input (they fall back to a neutral value) so the composite score is always defined.
    /// </summary>
    double Score(AggregatedCandidate candidate, ScoringContext context);
}
