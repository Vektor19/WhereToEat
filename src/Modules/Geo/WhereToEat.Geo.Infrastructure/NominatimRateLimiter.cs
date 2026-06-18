using Microsoft.Extensions.Options;

namespace WhereToEat.Geo.Infrastructure;

/// <summary>
/// The <b>process-wide</b> rate-limit gate in front of Nominatim. A single instance (registered as a
/// <b>singleton</b>) holds the one semaphore + the last-request timestamp for the whole process, so the
/// minimum interval is honoured across every concurrent caller and every retry — not per geocoder
/// instance. Nominatim's public usage policy is at most one request per second (invariant #9: respect
/// the rate-limit), and this is the single place that enforces it.
/// </summary>
/// <remarks>
/// Thread-safety: <see cref="AcquireAsync"/> serialises callers through the semaphore, then sleeps out
/// any remaining slice of the configured interval before returning a disposable lease. The caller MUST
/// dispose the lease (in a <c>finally</c>) after the outbound send completes; disposal stamps the
/// last-request time and releases the semaphore, so the next caller measures its wait from the moment
/// this request finished. Because the whole acquire→send→release path is serialised, there is no race
/// on the timestamp and no double-release.
/// </remarks>
public class NominatimRateLimiter : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly TimeProvider _timeProvider;
    private readonly Func<TimeSpan> _minIntervalProvider;

    private long _lastRequestTimestamp;
    private bool _hasSentRequest;

    /// <summary>Production constructor: reads the interval live from <see cref="NominatimOptions"/>.</summary>
    public NominatimRateLimiter(IOptions<NominatimOptions> options, TimeProvider? timeProvider = null)
        : this(
            () => TimeSpan.FromMilliseconds(Math.Max(0, (options ?? throw new ArgumentNullException(nameof(options))).Value.MinRequestIntervalMs)),
            timeProvider)
    {
    }

    /// <summary>Test/explicit constructor: the interval and clock are supplied directly.</summary>
    public NominatimRateLimiter(Func<TimeSpan> minIntervalProvider, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(minIntervalProvider);
        _minIntervalProvider = minIntervalProvider;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Waits for the gate, then waits out any remaining slice of the configured minimum interval since
    /// the previous request, and returns a lease that the caller disposes after the send completes.
    /// </summary>
    public virtual async ValueTask<IAsyncDisposable> AcquireAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await WaitForIntervalAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // If the interval wait is cancelled we must not leak the semaphore.
            _gate.Release();
            throw;
        }

        return new Lease(this);
    }

    private async Task WaitForIntervalAsync(CancellationToken cancellationToken)
    {
        var minInterval = _minIntervalProvider();
        if (!_hasSentRequest || minInterval <= TimeSpan.Zero)
        {
            return;
        }

        var elapsed = _timeProvider.GetElapsedTime(_lastRequestTimestamp);
        var remaining = minInterval - elapsed;
        if (remaining > TimeSpan.Zero)
        {
            await Task.Delay(remaining, _timeProvider, cancellationToken).ConfigureAwait(false);
        }
    }

    private void OnSendCompleted()
    {
        _lastRequestTimestamp = _timeProvider.GetTimestamp();
        _hasSentRequest = true;
        _gate.Release();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _gate.Dispose();
        }
    }

    /// <summary>The lease the caller disposes after its send — stamps the time and releases the gate.</summary>
    private sealed class Lease : IAsyncDisposable
    {
        private readonly NominatimRateLimiter _limiter;
        private bool _disposed;

        public Lease(NominatimRateLimiter limiter) => _limiter = limiter;

        public ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                _disposed = true;
                _limiter.OnSendCompleted();
            }

            return ValueTask.CompletedTask;
        }
    }
}
