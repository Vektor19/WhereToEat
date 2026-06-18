using WhereToEat.SharedKernel.Ratings;

namespace WhereToEat.Recommendation.Infrastructure.Candidates;

/// <summary>
/// Applies the <b>single authoritative</b> Bayesian smoothing (<see cref="BayesianRatingSmoothing"/>
/// in the SharedKernel) to the materialized aggregate's sum/count, with the prior weight <c>C</c> and
/// global mean <c>m</c> supplied by configuration (<see cref="RatingSmoothingSettings"/>). The rating
/// the recommendation engine ranks on therefore matches exactly what the Ratings recompute job
/// persists (same formula, same default constants). It is applied <b>here, in the candidate
/// source</b>, because the cross-module boundary forbids referencing <c>Ratings.Domain</c> — the
/// rating crosses only as the precomputed DTO field this produces from the joined inputs (SharedKernel
/// is referenceable by both modules, so the no-<c>Ratings</c>-reference fitness test stays green).
/// Pure arithmetic; <c>null</c> in (no aggregate) ⇒ <c>null</c> out.
/// </summary>
internal static class SmoothedRatingMath
{
    public static double? Smooth(double? sum, long? count, RatingSmoothingSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (sum is null || count is null)
        {
            // No materialized aggregate => no rating; the engine treats null as neutral, never 0.
            return null;
        }

        return BayesianRatingSmoothing.Smooth(sum.Value, count.Value, settings.PriorWeight, settings.GlobalMean);
    }
}

/// <summary>
/// The configurable smoothing constants the candidate source applies to the joined rating inputs:
/// the prior weight <c>C</c> and global mean <c>m</c> (invariant #6 — both configurable). Kept as a
/// small Infrastructure settings record so a host can bind them; the defaults are sourced from the
/// canonical SharedKernel constants (<see cref="BayesianRatingSmoothing.DefaultPriorWeight"/> = 10,
/// <see cref="BayesianRatingSmoothing.DefaultGlobalMean"/> = 3.5) — the same values
/// <c>RatingSmoothingOptions.Default</c> uses, so the engine and the recompute job never diverge.
/// </summary>
public sealed record RatingSmoothingSettings
{
    /// <summary>The prior weight <c>C</c> — virtual prior reviews pulling a venue toward the mean (≥ 0).</summary>
    public double PriorWeight { get; init; } = BayesianRatingSmoothing.DefaultPriorWeight;

    /// <summary>The global mean <c>m</c> — the neutral pull a low-count venue is smoothed toward.</summary>
    public double GlobalMean { get; init; } = BayesianRatingSmoothing.DefaultGlobalMean;
}
