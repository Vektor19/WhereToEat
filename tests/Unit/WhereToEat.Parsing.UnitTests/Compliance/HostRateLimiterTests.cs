using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using WhereToEat.Parsing.Infrastructure.Compliance;
using Xunit;

namespace WhereToEat.Parsing.UnitTests.Compliance;

/// <summary>
/// The per-host rate-limit gate (invariant #9). Backed by the in-memory counter store over a
/// <see cref="FakeTimeProvider"/>, so the interval is enforced deterministically without sleeping: the
/// first fetch of a host is allowed, a second within the interval is blocked, and a second after the
/// interval elapses is allowed again. Unrelated hosts are independent.
/// </summary>
public sealed class HostRateLimiterTests
{
    private static HostRateLimiter Build(FakeTimeProvider time, int intervalSeconds)
    {
        var store = new InMemoryRateLimitCounterStore(time);
        var options = Options.Create(new HostRateLimitOptions { MinIntervalSeconds = intervalSeconds });
        return new HostRateLimiter(store, options);
    }

    [Fact]
    public async Task FirstFetch_OfAHost_IsAllowed()
    {
        var limiter = Build(new FakeTimeProvider(), intervalSeconds: 10);

        (await limiter.TryAcquireAsync("borsch-cafe.example")).Should().BeTrue();
    }

    [Fact]
    public async Task SecondFetch_WithinTheInterval_IsBlocked()
    {
        var time = new FakeTimeProvider();
        var limiter = Build(time, intervalSeconds: 10);

        (await limiter.TryAcquireAsync("borsch-cafe.example")).Should().BeTrue();
        time.Advance(TimeSpan.FromSeconds(5));

        (await limiter.TryAcquireAsync("borsch-cafe.example"))
            .Should().BeFalse("the per-host minimum interval has not elapsed");
    }

    [Fact]
    public async Task Fetch_AfterTheInterval_IsAllowedAgain()
    {
        var time = new FakeTimeProvider();
        var limiter = Build(time, intervalSeconds: 10);

        (await limiter.TryAcquireAsync("borsch-cafe.example")).Should().BeTrue();
        time.Advance(TimeSpan.FromSeconds(10));

        (await limiter.TryAcquireAsync("borsch-cafe.example")).Should().BeTrue();
    }

    [Fact]
    public async Task DifferentHosts_AreIndependent()
    {
        var limiter = Build(new FakeTimeProvider(), intervalSeconds: 10);

        (await limiter.TryAcquireAsync("borsch-cafe.example")).Should().BeTrue();
        (await limiter.TryAcquireAsync("pizza-house.example"))
            .Should().BeTrue("the interval is per host, so an unrelated host is not throttled");
    }
}
