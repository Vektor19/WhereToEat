namespace WhereToEat.SharedKernel.Primitives;

/// <summary>
/// Marker for an event that records something that happened inside the domain
/// (e.g. an address changed). Aggregates raise these; the application layer dispatches
/// them. Kept in the SharedKernel so every module's domain can raise events without a
/// cross-module reference.
/// </summary>
public interface IDomainEvent
{
    /// <summary>When the event occurred (UTC).</summary>
    DateTimeOffset OccurredOnUtc { get; }
}
