using System.Diagnostics;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using WhereToEat.BuildingBlocks.Observability;
using WhereToEat.Contracts.IntegrationEvents;
using Xunit;

namespace WhereToEat.CrossCutting.Integration;

/// <summary>
/// Proves a request's correlation id survives a <b>publish → consume</b> hop (Step 13): the publisher
/// sets the id into the current <see cref="Activity"/>'s baggage under
/// <see cref="CorrelationId.PropertyName"/> exactly as <c>CorrelationIdMiddleware</c> does on the API
/// side; MassTransit carries the baggage on the message; and the consumer (running on its own Activity,
/// the worker side of the hop) reads back the SAME id. This is the API → bus → worker correlation the
/// design requires for cross-service traces.
/// </summary>
public sealed class CorrelationIdPropagationTests
{
    [Fact]
    public async Task Correlation_id_survives_a_publish_then_consume_hop()
    {
        var captured = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var provider = new ServiceCollection()
            .AddSingleton(captured)
            .AddLogging()
            .AddMassTransitTestHarness(cfg => cfg.AddConsumer<CorrelationCapturingConsumer>())
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            // The publisher side: ensure baggage actually flows by listening to the activity source, then
            // publish from inside an activity carrying the correlation id (what the middleware does).
            using var listener = new ActivityListener
            {
                ShouldListenTo = _ => true,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            };
            ActivitySource.AddActivityListener(listener);

            const string expected = "corr-7f3a9b2e";
            using var activity = new ActivitySource("test").StartActivity("publish");
            Activity.Current!.SetBaggage(CorrelationId.PropertyName, expected);

            await harness.Bus.Publish(new MenuUpdated(Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), 1));

            var seen = await captured.Task.WaitAsync(TimeSpan.FromSeconds(10));
            seen.Should().Be(expected, "the correlation id baggage must ride the bus to the consumer");
        }
        finally
        {
            await harness.Stop();
        }
    }

    /// <summary>A consumer that captures the correlation id from its own Activity's baggage on consume.</summary>
    private sealed class CorrelationCapturingConsumer : IConsumer<MenuUpdated>
    {
        private readonly TaskCompletionSource<string?> _captured;

        public CorrelationCapturingConsumer(TaskCompletionSource<string?> captured) => _captured = captured;

        public Task Consume(ConsumeContext<MenuUpdated> context)
        {
            var correlationId = Activity.Current?.GetBaggageItem(CorrelationId.PropertyName);
            _captured.TrySetResult(correlationId);
            return Task.CompletedTask;
        }
    }
}
