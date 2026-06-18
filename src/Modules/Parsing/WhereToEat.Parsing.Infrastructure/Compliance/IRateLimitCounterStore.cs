namespace WhereToEat.Parsing.Infrastructure.Compliance;

/// <summary>
/// The thin counter abstraction the <see cref="RedisHostRateLimiter"/> sits on so the rate-limit
/// state can live in <b>Redis</b> (shared across worker replicas) in production while falling back to
/// an <b>in-process</b> store when no Redis is configured. Step 13 supplies the Redis-backed
/// implementation behind the shared cache abstraction; Step 9 ships the in-memory fallback so the
/// gate is buildable/testable before any caching infrastructure exists. The contract is a single
/// atomic "claim a slot if the interval has elapsed" so multiple replicas cannot both pass.
/// </summary>
public interface IRateLimitCounterStore
{
    /// <summary>
    /// Atomically records a fetch for <paramref name="key"/> if at least <paramref name="interval"/>
    /// has elapsed since the last recorded fetch: returns <c>true</c> (slot claimed, "now" recorded)
    /// or <c>false</c> (too soon — no change). Atomicity is what makes the Redis implementation safe
    /// across replicas.
    /// </summary>
    Task<bool> TryClaimAsync(string key, TimeSpan interval, CancellationToken cancellationToken = default);
}
