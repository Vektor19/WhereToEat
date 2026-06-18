namespace WhereToEat.BuildingBlocks.Caching;

/// <summary>
/// The cross-cutting cache abstraction the hot read paths sit behind (taxonomy lists, the
/// <c>f_price</c> medians, rating aggregates) and the parser per-host rate-limit counters use. It is a
/// thin string-keyed get/set/remove + an atomic "claim a slot once per interval" primitive — enough
/// for the read-through caches and the distributed rate limiter without leaking a backend type.
/// <para>
/// <b>Backend-swappable by design:</b> the Step 13 implementation is Redis
/// (<see cref="RedisCacheService"/>), but callers depend only on this interface so a different store
/// (or an in-process fake in tests) drops in without touching call sites.
/// </para>
/// <para>
/// <b>Privacy invariants (CLAUDE.md #6/#7):</b> Google ratings/coordinates are <b>never</b> cached —
/// they are live-only through the Maps Embed. This interface is general-purpose; the rule is honoured
/// by what callers choose to store (no Google-sourced value is ever passed in), and it is asserted by
/// the cache guard test.
/// </para>
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Returns the cached string for <paramref name="key"/>, or <c>null</c> on a miss (or when the
    /// entry has expired). The caller is responsible for serialising/deserialising its own payload.
    /// </summary>
    Task<string?> GetStringAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores <paramref name="value"/> under <paramref name="key"/>, optionally expiring after
    /// <paramref name="expiry"/> (no expiry means the entry lives until evicted/removed). Overwrites
    /// an existing entry.
    /// </summary>
    Task SetStringAsync(
        string key,
        string value,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the entry for <paramref name="key"/> (a no-op when absent). Used by the cache-invalidation
    /// consumer when a menu changes (so a stale median/taxonomy entry does not survive a write).
    /// </summary>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes every entry whose key starts with <paramref name="prefix"/> (a namespace wipe). The
    /// invalidation consumer uses this to drop all price-median entries when a restaurant's menu
    /// changes (the medians are keyed by selection+area, not by restaurant, so a targeted delete is
    /// not possible — the namespace is cleared and rebuilt lazily on the next miss).
    /// </summary>
    Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically claims a slot for <paramref name="key"/> if at least <paramref name="interval"/> has
    /// elapsed since the last claim: returns <c>true</c> (slot claimed) or <c>false</c> (too soon). This
    /// is the distributed primitive the parser per-host rate-limit counter uses so multiple worker
    /// replicas cannot both fetch within the polite interval. The claim is set to expire after the
    /// interval so the key self-cleans.
    /// </summary>
    Task<bool> TryClaimSlotAsync(string key, TimeSpan interval, CancellationToken cancellationToken = default);
}
