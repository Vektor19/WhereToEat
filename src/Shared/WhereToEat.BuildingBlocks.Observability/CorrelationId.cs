namespace WhereToEat.BuildingBlocks.Observability;

/// <summary>
/// The shared correlation-id conventions so the public API, the bus, and the worker all read/write the
/// <b>same</b> header and log-property name — a request's id must survive the API → bus → worker hop
/// unchanged for the cross-service trace to line up. The header is the de-facto <c>X-Correlation-ID</c>;
/// the log property and OpenTelemetry baggage key are <c>CorrelationId</c>.
/// </summary>
public static class CorrelationId
{
    /// <summary>The inbound/outbound HTTP header carrying the correlation id.</summary>
    public const string HeaderName = "X-Correlation-ID";

    /// <summary>The structured-log property + baggage key the id is attached under.</summary>
    public const string PropertyName = "CorrelationId";
}
