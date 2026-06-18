namespace WhereToEat.Recommendation.Domain.Scoring;

/// <summary>
/// <c>f_price</c> — price-value relative to the market (CLAUDE.md §6): how cheap a venue's basket is
/// versus the <b>precomputed</b> per-area/category median (with city-wide fallback) supplied via the
/// <see cref="ScoringContext.MedianBasketAmount"/>. At the median the score is 0.5; cheaper trends
/// toward 1, pricier toward 0 (so "cheap" only means something relative to the local market). The
/// median is a <b>ready value</b> — never computed here, never per request (the
/// <c>IPriceMedianProvider</c> port owns it). With no median available, returns the neutral 0.5.
/// </summary>
public sealed class FPrice : IScoringFunction
{
    /// <summary>The factor key for the price function.</summary>
    public const string FactorKey = "price";

    /// <inheritdoc />
    public string Key => FactorKey;

    /// <inheritdoc />
    public double Score(AggregatedCandidate candidate, ScoringContext context)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(context);

        var median = context.MedianBasketAmount;

        // No market reference => "cheap relative to what?" is undefined; fall back to neutral so a
        // missing median never silently penalizes or rewards a venue.
        if (median is null || median.Value <= 0m)
        {
            return 0.5d;
        }

        var basket = (double)candidate.BasketPrice.Amount;
        var m = (double)median.Value;

        // ratio = basket / median. Map ratio 0 -> 1 (free), ratio 1 (at median) -> 0.5,
        // ratio 2 (twice the median) -> 0, capped. A simple, monotone, bounded normalizer:
        //   score = clamp(1 - ratio/2, 0, 1)
        var ratio = basket / m;
        var score = 1d - (ratio / 2d);

        return Normalization.Clamp01(score);
    }
}
