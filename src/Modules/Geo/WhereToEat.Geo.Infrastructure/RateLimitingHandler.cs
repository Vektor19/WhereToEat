namespace WhereToEat.Geo.Infrastructure;

/// <summary>
/// A <see cref="DelegatingHandler"/> that routes <b>every</b> outbound Nominatim request — including
/// each Polly retry — through the process-wide <see cref="NominatimRateLimiter"/> before it hits the
/// network. It is positioned as the <b>innermost</b> handler in the typed-client pipeline (Polly sits
/// outside it), so a Polly retry re-invokes this handler and therefore re-enters the rate limiter:
/// each attempt is throttled, never just the first (invariant #9: respect Nominatim's ≤ 1 req/s
/// policy). The lease is always released in a <c>finally</c>, so a failing send cannot wedge the gate.
/// </summary>
public sealed class RateLimitingHandler : DelegatingHandler
{
    private readonly NominatimRateLimiter _rateLimiter;

    public RateLimitingHandler(NominatimRateLimiter rateLimiter)
    {
        ArgumentNullException.ThrowIfNull(rateLimiter);
        _rateLimiter = rateLimiter;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var lease = await _rateLimiter.AcquireAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await lease.DisposeAsync().ConfigureAwait(false);
        }
    }
}
