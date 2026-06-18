namespace WhereToEat.Contracts.IntegrationEvents;

/// <summary>
/// Marker for a cross-module integration event published on the message bus (MassTransit,
/// in-process today). These are the only event shapes that cross module boundaries — a
/// consumer in another module depends on this contract, never on the publishing module's
/// domain internals (enforced by the Step 2 consumer-boundary fitness test).
/// </summary>
public interface IIntegrationEvent
{
    /// <summary>A unique id for this event occurrence (for idempotency/correlation).</summary>
    Guid EventId { get; }

    /// <summary>When the event occurred (UTC).</summary>
    DateTimeOffset OccurredOnUtc { get; }
}
