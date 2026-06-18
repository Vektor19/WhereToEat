namespace WhereToEat.Recommendation.Domain.Scoring;

/// <summary>
/// <c>f_quality</c> — our own rating quality (CLAUDE.md §6 / invariant #6). It reads the
/// <b>already-smoothed</b> Bayesian rating carried on the candidate
/// (<see cref="AggregatedCandidate.SmoothedRating"/>) and normalizes it to 0…1 against the rating
/// scale (5★). The smoothing toward neutral on low review counts is <b>already applied</b> by the
/// Ratings module before the value reaches here — this function consumes that value and never
/// recomputes it (the boundary that keeps the engine free of any <c>Ratings.Domain</c> reference).
/// A venue with no rating yet scores the configured neutral quality, not 0.
/// </summary>
public sealed class FQuality : IScoringFunction
{
    /// <summary>The factor key for the quality function.</summary>
    public const string FactorKey = "quality";

    /// <inheritdoc />
    public string Key => FactorKey;

    /// <inheritdoc />
    public double Score(AggregatedCandidate candidate, ScoringContext context)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(context);

        // No materialized rating => neutral, so an unrated venue is not treated as the worst venue.
        if (candidate.SmoothedRating is null)
        {
            return context.Options.NeutralNormalizedQuality;
        }

        var normalized = candidate.SmoothedRating.Value / context.Options.MaxNormalizedRating;
        return Normalization.Clamp01(normalized);
    }
}
