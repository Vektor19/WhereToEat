using Microsoft.Extensions.Options;
using WhereToEat.Parsing.Application.Compliance;

namespace WhereToEat.Parsing.Infrastructure.Compliance;

/// <summary>
/// Options for the per-host parse rate-limit (invariant #9 — honour rate-limit, do not hammer a
/// first-party site): the minimum interval that must elapse between two fetches of the same host.
/// </summary>
public sealed class HostRateLimitOptions
{
    /// <summary>The appsettings section these options bind to.</summary>
    public const string SectionName = "Parsing:RateLimit";

    /// <summary>
    /// The minimum interval, in seconds, between two fetches of the same host. Defaults to a polite
    /// 10 seconds; the weekly-parse worker (Step 12) spaces sources out far wider than this.
    /// </summary>
    public int MinIntervalSeconds { get; set; } = 10;
}

/// <summary>
/// The per-host rate-limit gate (invariant #9). It sits on the swappable
/// <see cref="IRateLimitCounterStore"/> seam — the in-process counter today
/// (<see cref="InMemoryRateLimitCounterStore"/>), and a <b>Redis</b>-backed counter behind the shared
/// cache abstraction once Step 13 lands, so the interval holds even across multiple worker replicas.
/// The "claim a slot if the interval has elapsed" decision is atomic in the store, so two replicas
/// cannot both pass for the same host.
///
/// (Note: the design split the planned <c>RedisHostRateLimiter</c> into this host-keyed limiter plus
/// the <see cref="IRateLimitCounterStore"/> seam — the Redis-vs-in-memory choice is the swappable
/// counter store, leaving this gate transport-agnostic.)
/// </summary>
public sealed class HostRateLimiter : IHostRateLimiter
{
    private readonly IRateLimitCounterStore _counterStore;
    private readonly TimeSpan _minInterval;

    public HostRateLimiter(IRateLimitCounterStore counterStore, IOptions<HostRateLimitOptions> options)
    {
        ArgumentNullException.ThrowIfNull(counterStore);
        ArgumentNullException.ThrowIfNull(options);
        _counterStore = counterStore;
        _minInterval = TimeSpan.FromSeconds(Math.Max(0, options.Value.MinIntervalSeconds));
    }

    /// <inheritdoc />
    public Task<bool> TryAcquireAsync(string host, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return Task.FromResult(false);
        }

        return _counterStore.TryClaimAsync($"parse:ratelimit:{host}", _minInterval, cancellationToken);
    }
}
