namespace WhereToEat.SharedKernel.Ratings;

/// <summary>
/// The <b>single authoritative</b> rating-smoothing math (invariant #6), living in the SharedKernel
/// alongside <see cref="Geo.Haversine"/> as pure, I/O-free math that any module may reference without
/// crossing a module boundary. It is the Bayesian additive-prior (a.k.a. "true Bayesian") average
/// <c>smoothed = (C*m + Sum) / (C + n)</c>, where:
/// <list type="bullet">
///   <item><c>Sum</c> — the all-time sum of a venue's raw scores;</item>
///   <item><c>n</c> — the venue's all-time review count;</item>
///   <item><c>C</c> — the prior weight (virtual prior reviews — <see cref="DefaultPriorWeight"/>);</item>
///   <item><c>m</c> — the global mean (the neutral pull — <see cref="DefaultGlobalMean"/>).</item>
/// </list>
///
/// <para>
/// Both the Ratings module (which persists the value via the Step 12 recompute job) and the
/// Recommendation module (which consumes it through the candidate-source DTO) call <b>this</b>
/// implementation with <b>these</b> constants, so the rating the engine ranks on can never drift
/// from the rating the recompute job stores. Because it sits in SharedKernel, neither module needs a
/// reference to the other (the Step 2 no-cross-module-reference fitness test stays green).
/// </para>
///
/// <para>
/// Guaranteed properties (the unit tests pin these): <c>n == 0</c> ⇒ exactly <c>m</c>;
/// <c>n → ∞</c> ⇒ the raw average <c>Sum/n</c>; a single high review with a non-trivial <c>C</c>
/// stays near <c>m</c> while many consistent reviews approach their raw average.
/// </para>
/// </summary>
public static class BayesianRatingSmoothing
{
    /// <summary>
    /// The canonical default prior weight <c>C</c> = 10 virtual reviews — a venue needs a meaningful
    /// sample before its raw average dominates the neutral mean.
    /// </summary>
    public const double DefaultPriorWeight = 10d;

    /// <summary>
    /// The canonical default global mean <c>m</c> = 3.5 on a 1–5 scale — the neutral value a venue
    /// with no reviews resolves to.
    /// </summary>
    public const double DefaultGlobalMean = 3.5d;

    /// <summary>
    /// Computes the smoothed rating from the all-time <paramref name="sum"/> and
    /// <paramref name="count"/> using the prior weight <paramref name="priorWeight"/> (<c>C</c>) and
    /// global mean <paramref name="globalMean"/> (<c>m</c>). With <c>C >= 0</c> the denominator
    /// <c>C + n</c> is zero only when both are zero — the no-review case, which returns
    /// <paramref name="globalMean"/> (never a misleading 0).
    /// </summary>
    /// <param name="sum">The all-time sum of raw scores (finite, non-negative).</param>
    /// <param name="count">The all-time number of reviews (non-negative).</param>
    /// <param name="priorWeight">The prior weight <c>C</c> (finite, non-negative).</param>
    /// <param name="globalMean">The global mean <c>m</c> (finite).</param>
    public static double Smooth(double sum, long count, double priorWeight, double globalMean)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "Review count cannot be negative.");
        }

        if (sum < 0d || !double.IsFinite(sum))
        {
            throw new ArgumentOutOfRangeException(nameof(sum), sum, "Rating sum must be finite and non-negative.");
        }

        if (priorWeight < 0d || !double.IsFinite(priorWeight))
        {
            throw new ArgumentOutOfRangeException(nameof(priorWeight), priorWeight, "Prior weight C must be finite and non-negative.");
        }

        if (!double.IsFinite(globalMean))
        {
            throw new ArgumentOutOfRangeException(nameof(globalMean), globalMean, "Global mean m must be finite.");
        }

        var denominator = priorWeight + count;

        // No reviews and no prior weight => the formula is 0/0; "no signal" resolves to the neutral
        // global mean (never a misleading 0). With any C > 0 this branch is not taken.
        if (denominator == 0d)
        {
            return globalMean;
        }

        return ((priorWeight * globalMean) + sum) / denominator;
    }
}
