namespace WhereToEat.Parsing.Application.Compliance;

/// <summary>
/// The per-host rate-limit gate (invariant #9 — honour rate-limit, do not hammer a first-party site).
/// Asked, <b>before any fetch</b>, whether enough time has elapsed since the last fetch of the same
/// host to fetch again. The counter is backed by the shared cache abstraction (Redis) in production
/// with an in-memory fallback, so the interval is enforced even across multiple worker replicas.
/// </summary>
public interface IHostRateLimiter
{
    /// <summary>
    /// Atomically checks-and-records a fetch for <paramref name="host"/>: returns <c>true</c> and
    /// records "now" when the configured minimum interval since the previous fetch has elapsed (the
    /// caller may fetch); returns <c>false</c> without recording when the host was fetched too
    /// recently (the caller must skip this run). Enforced per host so unrelated sources are
    /// independent.
    /// </summary>
    Task<bool> TryAcquireAsync(string host, CancellationToken cancellationToken = default);
}
