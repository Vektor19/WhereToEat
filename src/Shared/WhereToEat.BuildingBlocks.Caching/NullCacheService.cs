namespace WhereToEat.BuildingBlocks.Caching;

/// <summary>
/// The no-op <see cref="ICacheService"/> used when Redis is disabled/unconfigured: every read is a miss
/// and every write is discarded, so a read-through cache transparently degrades to its DB-only source
/// (Step 7's median provider, the taxonomy/rating reads). The slot-claim always grants the slot so a
/// host without Redis still functions (the in-process rate-limit fallback remains the gate there).
/// </summary>
public sealed class NullCacheService : ICacheService
{
    /// <inheritdoc />
    public Task<string?> GetStringAsync(string key, CancellationToken cancellationToken = default)
        => Task.FromResult<string?>(null);

    /// <inheritdoc />
    public Task SetStringAsync(
        string key,
        string value,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc />
    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc />
    public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc />
    public Task<bool> TryClaimSlotAsync(string key, TimeSpan interval, CancellationToken cancellationToken = default)
        => Task.FromResult(true);
}
