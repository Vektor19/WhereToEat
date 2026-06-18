using System.Globalization;

namespace WhereToEat.BuildingBlocks.Caching;

/// <summary>
/// The single place the cache-key conventions live so the read-through caches (the price-median
/// decorator, taxonomy/rating reads) and the invalidation consumer agree on the exact strings — a
/// key the writer uses and the invalidator deletes must be byte-identical. Keys are namespaced by a
/// stable prefix so unrelated entries never collide.
/// <para>
/// <b>Privacy:</b> no key derives from a Google-sourced rating/coordinate — those values are never
/// cached (CLAUDE.md #6/#7). Median/taxonomy/rating keys reference only our own ids/coarse area cells.
/// </para>
/// </summary>
public static class CacheKeys
{
    /// <summary>The taxonomy category-list cache key (a single deterministic list — invariant #1).</summary>
    public const string CategoryList = "catalog:categories";

    /// <summary>The dish-list-by-category cache key for <paramref name="categoryId"/>.</summary>
    public static string DishesByCategory(Guid categoryId)
        => string.Create(CultureInfo.InvariantCulture, $"catalog:dishes:{categoryId:N}");

    /// <summary>
    /// The price-median cache key for a selection-and-area basket. Built from a precomputed
    /// deterministic <paramref name="selectionAreaHash"/> so two equal selections in the same area
    /// share a cache entry. Namespaced under <c>recommendation:median:</c> so a per-restaurant menu
    /// change can wipe the whole median namespace via the invalidation consumer.
    /// </summary>
    public static string PriceMedian(string selectionAreaHash)
        => "recommendation:median:" + selectionAreaHash;

    /// <summary>The prefix every price-median key shares (the invalidation consumer's removal scope).</summary>
    public const string PriceMedianPrefix = "recommendation:median:";

    /// <summary>The rating-aggregate cache key for <paramref name="restaurantId"/>.</summary>
    public static string RatingAggregate(Guid restaurantId)
        => string.Create(CultureInfo.InvariantCulture, $"ratings:aggregate:{restaurantId:N}");
}
