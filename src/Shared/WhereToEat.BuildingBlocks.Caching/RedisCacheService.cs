using StackExchange.Redis;

namespace WhereToEat.BuildingBlocks.Caching;

/// <summary>
/// The <see cref="StackExchange.Redis"/>-backed <see cref="ICacheService"/>. Get/set/remove map onto
/// <c>StringGet</c>/<c>StringSet</c>/<c>KeyDelete</c>; the slot-claim uses an atomic server-side
/// <c>SET key value NX EX</c> (set-if-absent with an expiry) so a slot self-expires after the interval
/// and only the first caller in a window claims it — safe across worker replicas.
/// <para>
/// Backend state lives entirely in Redis; this class holds only the multiplexer-derived
/// <see cref="IDatabase"/>, so it is registered as a singleton (the multiplexer is the connection pool).
/// </para>
/// </summary>
public sealed class RedisCacheService : ICacheService
{
    private readonly IConnectionMultiplexer _multiplexer;

    public RedisCacheService(IConnectionMultiplexer multiplexer)
    {
        ArgumentNullException.ThrowIfNull(multiplexer);
        _multiplexer = multiplexer;
    }

    private IDatabase Database => _multiplexer.GetDatabase();

    /// <inheritdoc />
    public async Task<string?> GetStringAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();

        var value = await Database.StringGetAsync(key).ConfigureAwait(false);
        return value.IsNull ? null : value.ToString();
    }

    /// <inheritdoc />
    public async Task SetStringAsync(
        string key,
        string value,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        cancellationToken.ThrowIfCancellationRequested();

        await Database.StringSetAsync(key, value, expiry).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();

        await Database.KeyDeleteAsync(key).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        cancellationToken.ThrowIfCancellationRequested();

        // Enumerate matching keys per endpoint (SCAN under the hood — no blocking KEYS) and delete them.
        // Median entries are short-lived and few, so a namespace wipe is cheap; correctness (no stale
        // median survives a menu change) matters more than shaving a SCAN here.
        var database = Database;
        foreach (var endpoint in _multiplexer.GetEndPoints())
        {
            var server = _multiplexer.GetServer(endpoint);
            if (!server.IsConnected || server.IsReplica)
            {
                continue;
            }

            await foreach (var key in server.KeysAsync(pattern: prefix + "*").WithCancellation(cancellationToken)
                               .ConfigureAwait(false))
            {
                await database.KeyDeleteAsync(key).ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc />
    public async Task<bool> TryClaimSlotAsync(string key, TimeSpan interval, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();

        // SET key 1 NX EX <interval>: atomically set only if the key is absent, with a TTL equal to the
        // interval. The first caller in the window gets true and the key self-expires after the interval;
        // any caller arriving before expiry gets false. This is the cross-replica-safe claim.
        var ttl = interval <= TimeSpan.Zero ? TimeSpan.FromMilliseconds(1) : interval;
        return await Database
            .StringSetAsync(key, "1", ttl, When.NotExists)
            .ConfigureAwait(false);
    }
}
