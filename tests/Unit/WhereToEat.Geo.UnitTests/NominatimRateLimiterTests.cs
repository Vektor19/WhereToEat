using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using WhereToEat.Geo.Infrastructure;
using Xunit;

namespace WhereToEat.Geo.UnitTests;

/// <summary>
/// Step 8 DETERMINISTIC rate-limit tests. They use a <see cref="FakeTimeProvider"/> (no wall-clock
/// sleeps, CI-stable) to prove the process-wide gate:
/// <list type="bullet">
///   <item>the first request is never delayed;</item>
///   <item>a second request is GATED until the fake clock advances past the configured interval;</item>
///   <item>the gate is shared (one limiter instance enforces one outbound interval — process-wide);</item>
///   <item>EVERY HTTP attempt — including each Polly retry — re-enters the limiter (the pipeline is
///     built Polly-outer / rate-limit-inner), so a fast-failing 503 + retries does NOT breach the
///     interval.</item>
/// </list>
/// </summary>
public sealed class NominatimRateLimiterTests
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(1000);

    [Fact]
    public async Task FirstAcquire_IsNotDelayed()
    {
        var clock = new FakeTimeProvider();
        var limiter = new NominatimRateLimiter(() => Interval, clock);

        var acquire = limiter.AcquireAsync(CancellationToken.None);

        acquire.IsCompleted.Should().BeTrue("the first request has no prior request to wait behind");
        await (await acquire).DisposeAsync();
    }

    [Fact]
    public async Task SecondAcquire_IsGatedUntilTheFakeClockAdvancesPastTheInterval()
    {
        var clock = new FakeTimeProvider();
        var limiter = new NominatimRateLimiter(() => Interval, clock);

        // First request completes immediately and stamps the last-request time.
        await using (await limiter.AcquireAsync(CancellationToken.None))
        {
        }

        // The second request must wait out the interval. With the fake clock NOT advanced, the
        // delay never completes — proving the gate fires (not a wall-clock race).
        var second = limiter.AcquireAsync(CancellationToken.None).AsTask();
        second.IsCompleted.Should().BeFalse("the interval since the first request has not elapsed");

        // Advance just short of the interval — still gated.
        clock.Advance(Interval - TimeSpan.FromMilliseconds(1));
        second.IsCompleted.Should().BeFalse("the configured minimum interval has not fully elapsed");

        // Cross the interval — the gate opens deterministically.
        clock.Advance(TimeSpan.FromMilliseconds(1));
        var lease = await second;
        await lease.DisposeAsync();
    }

    [Fact]
    public async Task EveryHttpAttempt_IncludingPollyRetries_ReEntersTheLimiter()
    {
        // Build the SAME pipeline shape the module wires: Polly OUTER, rate-limit handler INNER,
        // fronted by a counting limiter. A fast-failing 503 forces Polly retries; because the
        // rate-limit handler sits INSIDE the retry handler, each retry re-invokes it and therefore
        // re-enters the limiter — so the gate is hit once per attempt, never bypassed. A zero interval
        // keeps this assertion free of any timing (the gating itself is proven deterministically by
        // SecondAcquire_IsGatedUntilTheFakeClockAdvancesPastTheInterval); together they show every
        // retry passes through — and re-throttles on — the limiter.
        var clock = new FakeTimeProvider();
        var limiter = new CountingRateLimiter(() => TimeSpan.Zero, clock);

        var innerHandler = new CountingMessageHandler(
            _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            clock);

        const int retryCount = 2;
        var rateLimitHandler = new RateLimitingHandler(limiter) { InnerHandler = innerHandler };
        var pollyHandler = new TestRetryHandler(retryCount) { InnerHandler = rateLimitHandler };

        using var invoker = new HttpMessageInvoker(pollyHandler);

        using var request = new HttpRequestMessage(HttpMethod.Get, "https://nominatim.test/search");
        using var response = await invoker.SendAsync(request, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        innerHandler.RequestCount.Should().Be(retryCount + 1, "Polly retried the failing send");
        limiter.AcquireCount.Should().Be(
            retryCount + 1,
            "every HTTP attempt — first + each Polly retry — must pass through the rate limiter (Polly outer, rate-limit inner)");
    }

    /// <summary>A limiter that counts how many times the gate was entered (proves per-attempt re-entry).</summary>
    private sealed class CountingRateLimiter : NominatimRateLimiter
    {
        private int _acquireCount;

        public CountingRateLimiter(Func<TimeSpan> minIntervalProvider, TimeProvider timeProvider)
            : base(minIntervalProvider, timeProvider)
        {
        }

        public int AcquireCount => _acquireCount;

        public override async ValueTask<IAsyncDisposable> AcquireAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _acquireCount);
            return await base.AcquireAsync(cancellationToken);
        }
    }

    /// <summary>
    /// A minimal stand-in for the Polly retry handler: retries the inner send up to
    /// <c>retryCount</c> times on a transient (5xx) response, exercising the OUTER position so each
    /// retry re-invokes the inner rate-limit handler — the exact ordering the module relies on.
    /// </summary>
    private sealed class TestRetryHandler : DelegatingHandler
    {
        private readonly int _retryCount;

        public TestRetryHandler(int retryCount) => _retryCount = retryCount;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
            for (var attempt = 0; attempt < _retryCount && (int)response.StatusCode >= 500; attempt++)
            {
                response.Dispose();
                response = await base.SendAsync(request, cancellationToken);
            }

            return response;
        }
    }
}
