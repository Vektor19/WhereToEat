using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using WhereToEat.BuildingBlocks.Caching;
using WhereToEat.Contracts.Recommendation;
using WhereToEat.Recommendation.Application.Abstractions;

namespace WhereToEat.Recommendation.Infrastructure.Medians;

/// <summary>
/// The Step 13 <b>transparent caching decorator</b> over the Step 7 <see cref="DbPriceMedianProvider"/>
/// (CLAUDE.md §6, <c>f_price</c>): a cache hit returns the cached median, a miss reads the DB median
/// table through the wrapped provider and populates the cache. It implements the <b>same
/// <see cref="IPriceMedianProvider"/> contract</b> and returns <b>identical values</b> to the DB-only
/// provider — so Step 7's recommendation results are unchanged whether this decorator is registered or
/// not. The DB stays the source of truth; the cache only avoids re-reading a ready value.
/// <para>
/// The cache key is a stable hash of the selection (category/dish ids, order-independent) + the coarse
/// area cell, so two equal requests share an entry. A menu change wipes the median namespace via the
/// <c>InvalidateCacheOnMenuUpdatedConsumer</c> (the cache is rebuilt lazily on the next miss).
/// </para>
/// <para>
/// <b>Privacy:</b> only our own precomputed median value + our own ids/coarse cell are cached — no
/// Google rating/coordinate is ever stored (CLAUDE.md #6/#7).
/// </para>
/// </summary>
public sealed class CachingPriceMedianProvider : IPriceMedianProvider
{
    // A null DB median is also cached (as this sentinel) so a "no median yet" answer does not re-hit
    // the DB on every request; it is invalidated alongside the real values when a menu changes.
    private const string NullSentinel = "null";

    // Medians change only on the nightly refresh / a menu update (which invalidates), so a modest TTL
    // bounds staleness even if an invalidation message is ever missed.
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(6);

    private readonly DbPriceMedianProvider _inner;
    private readonly ICacheService _cache;

    public CachingPriceMedianProvider(DbPriceMedianProvider inner, ICacheService cache)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(cache);
        _inner = inner;
        _cache = cache;
    }

    /// <inheritdoc />
    public async Task<decimal?> GetMedianBasketAmountAsync(
        IReadOnlyList<SelectedItem> items,
        UserGeo? userGeo,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0)
        {
            // Identical to the DB-only provider's early return — no key, no DB call.
            return null;
        }

        var cacheKey = CacheKeys.PriceMedian(BuildSelectionAreaHash(items, userGeo));

        var cached = await _cache.GetStringAsync(cacheKey, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            // Cache hit — return without touching the DB. NullSentinel maps back to a null median.
            return ParseCached(cached);
        }

        // Miss: read the DB median table through the Step 7 provider, then populate the cache.
        var fromDb = await _inner.GetMedianBasketAmountAsync(items, userGeo, cancellationToken).ConfigureAwait(false);

        var toCache = fromDb is null
            ? NullSentinel
            : fromDb.Value.ToString(CultureInfo.InvariantCulture);
        await _cache.SetStringAsync(cacheKey, toCache, CacheTtl, cancellationToken).ConfigureAwait(false);

        return fromDb;
    }

    private static decimal? ParseCached(string cached)
        => string.Equals(cached, NullSentinel, StringComparison.Ordinal)
            ? null
            : decimal.Parse(cached, CultureInfo.InvariantCulture);

    private static string BuildSelectionAreaHash(IReadOnlyList<SelectedItem> items, UserGeo? userGeo)
    {
        // Order-independent: two requests with the same set of selections (any order) share an entry.
        var tokens = items
            .Select(i => i.DishId is not null
                ? "d:" + i.DishId.Value.ToString("N")
                : "c:" + i.CategoryId!.Value.ToString("N"))
            .OrderBy(t => t, StringComparer.Ordinal);

        var payload = string.Join('|', tokens) + "@" + AreaKey.Resolve(userGeo);

        // A short stable hash keeps keys compact and free of separator/encoding surprises.
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(digest, 0, 12);
    }
}
