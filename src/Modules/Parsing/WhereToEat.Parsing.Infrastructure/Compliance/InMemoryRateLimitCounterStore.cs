using System.Collections.Concurrent;

namespace WhereToEat.Parsing.Infrastructure.Compliance;

/// <summary>
/// The in-process fallback <see cref="IRateLimitCounterStore"/> (the documented fallback when no
/// Redis is configured — Step 13 adds the Redis-backed store behind the shared cache abstraction).
/// It keeps the last-claim instant per key in a concurrent map and claims a slot atomically under a
/// per-key compare-and-swap, so concurrent callers in one process cannot both pass the interval. Time
/// comes from an injected <see cref="TimeProvider"/> so the interval is testable without sleeping.
/// </summary>
public sealed class InMemoryRateLimitCounterStore : IRateLimitCounterStore
{
    private readonly ConcurrentDictionary<string, long> _lastClaimTicks = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;

    public InMemoryRateLimitCounterStore(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public Task<bool> TryClaimAsync(string key, TimeSpan interval, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcTicks;
        var claimed = false;

        _lastClaimTicks.AddOrUpdate(
            key,
            _ =>
            {
                claimed = true;
                return now;
            },
            (_, previous) =>
            {
                if (now - previous >= interval.Ticks)
                {
                    claimed = true;
                    return now;
                }

                claimed = false;
                return previous;
            });

        return Task.FromResult(claimed);
    }
}
