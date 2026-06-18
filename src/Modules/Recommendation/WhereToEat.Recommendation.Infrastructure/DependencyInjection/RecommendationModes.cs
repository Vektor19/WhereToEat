using WhereToEat.Recommendation.Domain;
using WhereToEat.Recommendation.Domain.Scoring;
using WhereToEat.Recommendation.Domain.Sorting;

namespace WhereToEat.Recommendation.Infrastructure.DependencyInjection;

/// <summary>
/// The per-mode tuning the composition wires in: the composite-mode factor weights (CLAUDE.md §6
/// Step 3 (B) — "for price-quality the weights balance price and quality; for best they spread")
/// and the <see cref="RecommendationModeOptions"/> each mode runs under. Centralised here so the
/// weights are reviewable as one table and a host could later bind/override them from config; the
/// values are the committed defaults. Changing a weight changes the composite ordering — the
/// weight-sensitivity the unit tests pin.
/// </summary>
internal static class RecommendationModes
{
    /// <summary>The neutral default options used for explicit sorts and any unmapped mode.</summary>
    public static RecommendationModeOptions DefaultOptions { get; } = new();

    /// <summary>
    /// "Price-quality": price and quality carry the weight; distance is a light tie-influencer and
    /// coverage a small doweight, so the mode is genuinely a price/quality compromise.
    /// </summary>
    public static IReadOnlyDictionary<string, double> PriceQualityWeights { get; } =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            [FPrice.FactorKey] = 0.45d,
            [FQuality.FactorKey] = 0.45d,
            [FDistance.FactorKey] = 0.05d,
            [FCoverage.FactorKey] = 0.05d,
        };

    /// <summary>
    /// "Best": weight spread across all four facets (the all-round mode), with distance and coverage
    /// carrying meaningful but not dominant weight.
    /// </summary>
    public static IReadOnlyDictionary<string, double> BestWeights { get; } =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            [FPrice.FactorKey] = 0.3d,
            [FQuality.FactorKey] = 0.3d,
            [FDistance.FactorKey] = 0.25d,
            [FCoverage.FactorKey] = 0.15d,
        };

    /// <summary>The options each sort mode runs under (all share the neutral normalization defaults).</summary>
    public static IReadOnlyDictionary<string, RecommendationModeOptions> OptionsByKey { get; } =
        new Dictionary<string, RecommendationModeOptions>(StringComparer.OrdinalIgnoreCase)
        {
            [ExplicitSortStrategy.PriceKey] = DefaultOptions,
            [ExplicitSortStrategy.DistanceKey] = DefaultOptions,
            [ExplicitSortStrategy.RatingKey] = DefaultOptions,
            [CompositeScoreStrategy.PriceQualityKey] = DefaultOptions,
            [CompositeScoreStrategy.BestKey] = DefaultOptions,
        };
}
