using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace WhereToEat.BuildingBlocks.Observability;

/// <summary>
/// Ensures every request carries a correlation id and makes it visible to the rest of the stack:
/// it reads the inbound <c>X-Correlation-ID</c> header (or mints a new GUID when absent), echoes it on
/// the response, attaches it as the current <see cref="Activity"/>'s baggage (so it rides the
/// OpenTelemetry trace and any outbound publish — the API → bus → worker hop), and pushes it into a
/// logging scope so every log line for the request includes the same id.
/// </summary>
public sealed partial class CorrelationIdMiddleware : IMiddleware
{
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(ILogger<CorrelationIdMiddleware> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var correlationId = ResolveCorrelationId(context);

        // Echo it back so a caller can correlate its own logs with ours.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationId.HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        // Baggage flows across process boundaries (the bus publish copies it), so the worker that
        // consumes a message published during this request sees the same id on its Activity.
        Activity.Current?.SetBaggage(CorrelationId.PropertyName, correlationId);

        using (_logger.BeginScope(new Dictionary<string, object> { [CorrelationId.PropertyName] = correlationId }))
        {
            await next(context).ConfigureAwait(false);
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationId.HeaderName, out var header))
        {
            var value = header.ToString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
    }
}
