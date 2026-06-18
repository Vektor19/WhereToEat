using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using WhereToEat.BuildingBlocks.Caching;
using WhereToEat.BuildingBlocks.Messaging.Consumers;
using WhereToEat.Contracts.Geo;
using WhereToEat.Contracts.IntegrationEvents;
using Xunit;

namespace WhereToEat.CrossCutting.Integration;

/// <summary>
/// Drives the two Step 13 consumers through the MassTransit <b>in-memory test harness</b> (no broker):
/// the geocode-on-<see cref="RestaurantAddressChanged"/> consumer invokes the Contracts
/// <see cref="IRestaurantGeocoder"/> seam, and the invalidate-cache-on-<see cref="MenuUpdated"/> consumer
/// invokes the <see cref="ICacheService"/> abstraction — both depending ONLY on Contracts / shared
/// abstractions (the Step 2 consumer-boundary rule), proven here because the test wires nothing but
/// those seams.
/// </summary>
public sealed class MessagingConsumerTests
{
    [Fact]
    public async Task GeocodeConsumer_handles_AddressChanged_and_calls_the_geocoder()
    {
        var geocoder = Substitute.For<IRestaurantGeocoder>();
        geocoder.GeocodeAndStoreAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);

        await using var provider = new ServiceCollection()
            .AddSingleton(geocoder)
            .AddLogging()
            .AddMassTransitTestHarness(cfg => cfg.AddConsumer<GeocodeOnAddressChangedConsumer>())
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            var restaurantId = Guid.NewGuid();
            await harness.Bus.Publish(new RestaurantAddressChanged(Guid.NewGuid(), DateTimeOffset.UtcNow, restaurantId));

            (await harness.Consumed.Any<RestaurantAddressChanged>()).Should().BeTrue();
            await geocoder.Received(1).GeocodeAndStoreAsync(restaurantId, Arg.Any<CancellationToken>());
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task InvalidateCacheConsumer_handles_MenuUpdated_and_wipes_the_median_namespace()
    {
        var cache = Substitute.For<ICacheService>();

        await using var provider = new ServiceCollection()
            .AddSingleton(cache)
            .AddLogging()
            .AddMassTransitTestHarness(cfg => cfg.AddConsumer<InvalidateCacheOnMenuUpdatedConsumer>())
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(new MenuUpdated(Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), 3));

            (await harness.Consumed.Any<MenuUpdated>()).Should().BeTrue();
            await cache.Received(1).RemoveByPrefixAsync(CacheKeys.PriceMedianPrefix, Arg.Any<CancellationToken>());
            await cache.Received(1).RemoveAsync(CacheKeys.CategoryList, Arg.Any<CancellationToken>());
        }
        finally
        {
            await harness.Stop();
        }
    }
}
