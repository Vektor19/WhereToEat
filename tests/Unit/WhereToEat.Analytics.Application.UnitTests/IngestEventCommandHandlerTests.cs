using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WhereToEat.Analytics.Application;
using WhereToEat.Analytics.Domain;
using WhereToEat.Analytics.Domain.Abstractions;
using WhereToEat.Analytics.Domain.Anonymization;
using WhereToEat.SharedKernel.Results;
using Xunit;

namespace WhereToEat.Analytics.Application.UnitTests;

/// <summary>
/// Proves the <see cref="IngestEventCommandHandler"/> anonymizes <b>before</b> persisting and never
/// hands a raw payload to the store: a failed anonymization writes nothing (all-or-nothing per call),
/// and a successful batch is appended via the append-optimized writer.
/// </summary>
public sealed class IngestEventCommandHandlerTests
{
    private readonly IAnonymizer _anonymizer = Substitute.For<IAnonymizer>();
    private readonly IAnalyticsEventStore _store = Substitute.For<IAnalyticsEventStore>();
    private readonly IngestEventCommandHandler _handler;

    public IngestEventCommandHandlerTests()
        => _handler = new IngestEventCommandHandler(_anonymizer, _store, NullLogger<IngestEventCommandHandler>.Instance);

    private static AnalyticsEvent AnyAnonymizedEvent()
        => AnalyticsEvent.Record(
            EventKind.Impression,
            HourBucket.FromInstant(DateTimeOffset.UtcNow),
            EventDimensions.Create(restaurantId: Guid.NewGuid())).Value;

    [Fact]
    public async Task Handle_Anonymizes_ThenAppendsTheBatch()
    {
        _anonymizer.Anonymize(Arg.Any<RawAnalyticsEvent>()).Returns(Result.Success(AnyAnonymizedEvent()));

        var command = new IngestEventCommand(
        [
            new RawAnalyticsEvent { Kind = EventKind.Impression },
            new RawAnalyticsEvent { Kind = EventKind.CardOpen },
        ]);

        var result = await _handler.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();
        await _store.Received(1).AppendBatchAsync(
            Arg.Is<IReadOnlyCollection<AnalyticsEvent>>(e => e.Count == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenAnonymizationFails_WritesNothing()
    {
        _anonymizer.Anonymize(Arg.Any<RawAnalyticsEvent>())
            .Returns(Result.Failure<AnalyticsEvent>(Error.Validation("Analytics.InvalidLocation", "bad coord")));

        var command = new IngestEventCommand([new RawAnalyticsEvent { Kind = EventKind.Search, Latitude = 999 }]);

        var result = await _handler.HandleAsync(command);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Analytics.InvalidLocation");
        await _store.DidNotReceiveWithAnyArgs().AppendBatchAsync(default!, default);
    }

    [Fact]
    public async Task Handle_RejectsAnEmptyBatch()
    {
        var result = await _handler.HandleAsync(new IngestEventCommand(Array.Empty<RawAnalyticsEvent>()));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Analytics.NoEvents");
        await _store.DidNotReceiveWithAnyArgs().AppendBatchAsync(default!, default);
    }
}
