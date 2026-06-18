using FluentAssertions;
using WhereToEat.SharedKernel.Primitives;
using Xunit;

namespace WhereToEat.SharedKernel.UnitTests.Primitives;

public sealed class AggregateRootTests
{
    private sealed record TestEvent(DateTimeOffset OccurredOnUtc) : IDomainEvent;

    // A test aggregate that surfaces the protected RaiseDomainEvent so the collection
    // behavior (add / read-only exposure / clear) can be exercised.
    private sealed class TestAggregate : AggregateRoot<Guid>
    {
        public TestAggregate()
            : base(Guid.NewGuid())
        {
        }

        public void DoSomething(IDomainEvent domainEvent) => RaiseDomainEvent(domainEvent);
    }

    [Fact]
    public void RaiseDomainEvent_AddsEventInOrder()
    {
        var aggregate = new TestAggregate();
        var first = new TestEvent(DateTimeOffset.UnixEpoch);
        var second = new TestEvent(DateTimeOffset.UnixEpoch.AddHours(1));

        aggregate.DoSomething(first);
        aggregate.DoSomething(second);

        aggregate.DomainEvents.Should().Equal(first, second);
    }

    [Fact]
    public void NewAggregate_HasNoDomainEvents()
    {
        var aggregate = new TestAggregate();

        aggregate.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void RaiseDomainEvent_Null_Throws()
    {
        var aggregate = new TestAggregate();

        var act = () => aggregate.DoSomething(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void DomainEvents_IsReadOnlyView_NotCastableToMutableList()
    {
        var aggregate = new TestAggregate();
        aggregate.DoSomething(new TestEvent(DateTimeOffset.UnixEpoch));

        // The exposed collection is a read-only wrapper, not the backing List<T>, so callers
        // cannot mutate the aggregate's events by casting the property.
        aggregate.DomainEvents.Should().BeAssignableTo<IReadOnlyCollection<IDomainEvent>>();
        (aggregate.DomainEvents is List<IDomainEvent>).Should().BeFalse();
    }

    [Fact]
    public void ClearDomainEvents_EmptiesTheCollection()
    {
        var aggregate = new TestAggregate();
        aggregate.DoSomething(new TestEvent(DateTimeOffset.UnixEpoch));
        aggregate.DoSomething(new TestEvent(DateTimeOffset.UnixEpoch.AddMinutes(5)));

        aggregate.ClearDomainEvents();

        aggregate.DomainEvents.Should().BeEmpty();
    }
}
