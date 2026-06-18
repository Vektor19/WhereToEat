using WhereToEat.SharedKernel.Ratings;

namespace WhereToEat.Ratings.Domain;

/// <summary>
/// The Ratings module's view of the <b>single authoritative</b> rating-smoothing formula
/// (invariant #6): the Bayesian additive-prior (a.k.a. "true Bayesian") average
/// <c>smoothed = (C*m + Sum) / (C + n)</c>, where:
/// <list type="bullet">
///   <item><c>Sum</c> — the all-time sum of a venue's raw scores;</item>
///   <item><c>n</c> — the venue's all-time review count;</item>
///   <item><c>C</c> — the configurable prior weight (virtual prior reviews);</item>
///   <item><c>m</c> — the configurable global mean (the neutral pull).</item>
/// </list>
///
/// <para>
/// Properties this guarantees (and the unit tests pin):
/// <list type="bullet">
///   <item><b>n == 0</b> -> the result is exactly <c>m</c> (no reviews => neutral, never 0).</item>
///   <item><b>n -> infinity</b> -> the result approaches the raw average <c>Sum/n</c> (the prior washes out).</item>
///   <item>a single high review with a non-trivial <c>C</c> stays close to <c>m</c>, while many
///   consistent reviews approach their raw average — so one lucky 5★ never outranks a stable 4.5★
///   over hundreds of reviews.</item>
/// </list>
/// </para>
///
/// Pure static math with no state and no I/O. The actual formula lives once in
/// <see cref="BayesianRatingSmoothing"/> in the SharedKernel; this type simply <b>delegates</b> to it
/// (and exposes the convenience options overload) so the Ratings domain, the Step 12 recompute job,
/// and the recommendation engine all compute the identical value from the identical constants — no
/// divergent re-implementation.
/// </summary>
public static class BayesianSmoothing
{
    /// <summary>
    /// Computes the smoothed rating from the all-time <paramref name="sum"/> and
    /// <paramref name="count"/> using the configurable prior weight <paramref name="priorWeight"/>
    /// (<c>C</c>) and global mean <paramref name="globalMean"/> (<c>m</c>). Delegates to the single
    /// authoritative <see cref="BayesianRatingSmoothing.Smooth"/> in the SharedKernel.
    /// </summary>
    /// <param name="sum">The all-time sum of raw scores (non-negative).</param>
    /// <param name="count">The all-time number of reviews (non-negative).</param>
    /// <param name="priorWeight">The prior weight <c>C</c> (finite, non-negative).</param>
    /// <param name="globalMean">The global mean <c>m</c> (the neutral pull).</param>
    public static double Smooth(double sum, long count, double priorWeight, double globalMean) =>
        BayesianRatingSmoothing.Smooth(sum, count, priorWeight, globalMean);

    /// <summary>
    /// Convenience overload reading the prior weight and global mean from
    /// <see cref="RatingSmoothingOptions"/>.
    /// </summary>
    public static double Smooth(double sum, long count, RatingSmoothingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return Smooth(sum, count, options.PriorWeight, options.GlobalMean);
    }
}
