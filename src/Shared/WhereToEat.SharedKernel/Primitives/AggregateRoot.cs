namespace WhereToEat.SharedKernel.Primitives;

/// <summary>
/// Base class for aggregate roots — the consistency boundary that owns child entities and
/// is the only thing repositories load/save. Collects <see cref="IDomainEvent"/>s raised
/// during a unit of work for the application layer to dispatch after persistence.
/// </summary>
/// <typeparam name="TId">The strongly-typed identifier type.</typeparam>
public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    protected AggregateRoot(TId id)
        : base(id)
    {
    }

    /// <summary>The domain events raised on this aggregate and not yet dispatched.</summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>Records a domain event to be dispatched after the aggregate is persisted.</summary>
    protected void RaiseDomainEvent(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Add(domainEvent);
    }

    /// <summary>Clears the recorded domain events (called once they have been dispatched).</summary>
    public void ClearDomainEvents() => _domainEvents.Clear();
}
