namespace WhereToEat.Recommendation.Domain.Scoring;

/// <summary>
/// <c>f_distance</c> — closeness (CLAUDE.md §6): turns the exact Haversine distance into a 0…1 score
/// that decays linearly from 1 (at the user) to 0 (at/over the configured radius). Distance is
/// <b>on by default</b> but user-disable-able: when disabled (the user omitted their location), this
/// returns a neutral 1 so it does not penalize any venue and the other facets decide. A venue with
/// no stored location also scores neutral rather than worst.
/// </summary>
public sealed class FDistance : IScoringFunction
{
    /// <summary>The factor key for the distance function.</summary>
    public const string FactorKey = "distance";

    /// <inheritdoc />
    public string Key => FactorKey;

    /// <inheritdoc />
    public double Score(AggregatedCandidate candidate, ScoringContext context)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(context);

        // Distance off (user disabled/omitted geo) or no known distance => neutral 1: distance is
        // simply not a factor, the other f_* decide. This is the user-disable-able behaviour.
        if (!context.DistanceEnabled || candidate.DistanceKm is null)
        {
            return 1d;
        }

        var radius = context.Options.DistanceNormalizationRadiusKm;
        var score = 1d - (candidate.DistanceKm.Value / radius);
        return Normalization.Clamp01(score);
    }
}
