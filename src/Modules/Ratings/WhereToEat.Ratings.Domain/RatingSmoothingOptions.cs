using WhereToEat.SharedKernel.Ratings;

namespace WhereToEat.Ratings.Domain;

/// <summary>
/// The two <b>configurable</b> inputs to the Bayesian additive-prior smoothing (invariant #6):
/// the prior weight <c>C</c> and the global mean <c>m</c>. They are options (not hard-coded) so the
/// smoothing can be tuned as the rating corpus grows — e.g. raising <c>C</c> demands more reviews
/// before a venue's average is trusted, and <c>m</c> tracks the system-wide mean.
///
/// <para>
/// The formula they feed is <c>smoothed = (C*m + Sum) / (C + n)</c>, where <c>Sum</c> is the all-time
/// sum of a venue's raw scores and <c>n</c> its review count. This is the single authoritative
/// quality value <c>f_quality</c> (Step 7) consumes.
/// </para>
/// </summary>
public sealed class RatingSmoothingOptions
{
    /// <summary>
    /// Creates the smoothing options, rejecting a negative prior weight and a global mean outside the
    /// rating scale. <paramref name="priorWeight"/> is <c>C</c> — the number of "virtual" prior
    /// reviews pulling a low-count average toward <paramref name="globalMean"/>;
    /// <paramref name="globalMean"/> is <c>m</c>, the neutral value a venue with no reviews resolves to.
    /// </summary>
    public RatingSmoothingOptions(double priorWeight, double globalMean)
    {
        if (priorWeight < 0d || !double.IsFinite(priorWeight))
        {
            throw new ArgumentOutOfRangeException(
                nameof(priorWeight), priorWeight, "The prior weight C must be a finite, non-negative value.");
        }

        if (!double.IsFinite(globalMean) || globalMean < Rating.MinScore || globalMean > Rating.MaxScore)
        {
            throw new ArgumentOutOfRangeException(
                nameof(globalMean), globalMean, $"The global mean m must lie within the rating scale [{Rating.MinScore}, {Rating.MaxScore}].");
        }

        PriorWeight = priorWeight;
        GlobalMean = globalMean;
    }

    /// <summary>The prior weight <c>C</c> — virtual prior reviews. Higher = more smoothing.</summary>
    public double PriorWeight { get; }

    /// <summary>The global mean <c>m</c> — the neutral value a no-review venue resolves to.</summary>
    public double GlobalMean { get; }

    /// <summary>
    /// A sensible default for a 1–5 scale, sourced from the canonical SharedKernel constants
    /// (<see cref="BayesianRatingSmoothing.DefaultPriorWeight"/> = 10 virtual reviews,
    /// <see cref="BayesianRatingSmoothing.DefaultGlobalMean"/> = 3.5 neutral mean) — the same
    /// constants the recommendation engine smooths with, so there is exactly one set of defaults.
    /// Real values are configured per environment; this keeps domain unit tests and seeds simple.
    /// </summary>
    public static RatingSmoothingOptions Default { get; } = new(
        priorWeight: BayesianRatingSmoothing.DefaultPriorWeight,
        globalMean: BayesianRatingSmoothing.DefaultGlobalMean);
}
