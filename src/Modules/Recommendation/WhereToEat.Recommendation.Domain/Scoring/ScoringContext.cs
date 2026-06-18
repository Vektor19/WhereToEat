namespace WhereToEat.Recommendation.Domain.Scoring;

/// <summary>
/// The external inputs the scoring functions need beyond the candidate itself: the per-mode
/// <see cref="RecommendationModeOptions"/> (weights, distance radius, rating scale), the
/// <b>precomputed</b> market-median basket price that <c>f_price</c> compares against, the maximum
/// coverage in this request (the selection count, for normalizing <c>f_coverage</c>), and whether
/// distance ranking is enabled. Everything here is a <b>ready value</b> — the median is read once
/// from the <c>IPriceMedianProvider</c> port up in the Application layer and passed in; the Domain
/// never computes a median per request (CLAUDE.md §6 <c>f_price</c> note).
/// </summary>
public sealed record ScoringContext
{
    /// <summary>Creates the scoring context for one recommendation request.</summary>
    /// <param name="options">The per-mode weights and normalization parameters.</param>
    /// <param name="medianBasketAmount">
    /// The precomputed market-median basket amount <c>f_price</c> compares each candidate against
    /// (per-area/category median with city-wide fallback, supplied by the median port). <c>null</c>
    /// when no median is available — <c>f_price</c> then returns its neutral value.
    /// </param>
    /// <param name="maxCoverage">The selection count (the highest possible coverage) for normalizing coverage.</param>
    /// <param name="distanceEnabled">Whether distance ranking is on (false when the user omitted/disabled geo).</param>
    public ScoringContext(
        RecommendationModeOptions options,
        decimal? medianBasketAmount,
        int maxCoverage,
        bool distanceEnabled)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (maxCoverage < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCoverage), maxCoverage, "Max coverage cannot be negative.");
        }

        Options = options;
        MedianBasketAmount = medianBasketAmount;
        MaxCoverage = maxCoverage;
        DistanceEnabled = distanceEnabled;
    }

    /// <summary>The per-mode weights and normalization parameters.</summary>
    public RecommendationModeOptions Options { get; }

    /// <summary>The precomputed market-median basket amount, or <c>null</c> when none is available.</summary>
    public decimal? MedianBasketAmount { get; }

    /// <summary>The selection count (the highest possible coverage) for this request.</summary>
    public int MaxCoverage { get; }

    /// <summary>Whether distance ranking is enabled for this request.</summary>
    public bool DistanceEnabled { get; }
}
