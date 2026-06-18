using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WhereToEat.BuildingBlocks.Messaging.Consumers;

namespace WhereToEat.BuildingBlocks.Messaging;

/// <summary>
/// Wires MassTransit a host calls from its composition root. The transport is chosen <b>by
/// configuration</b> (<see cref="MessagingOptions.Transport"/>): the <b>in-process transport now</b>
/// (no broker dependency) with a <b>RabbitMQ-ready</b> path that connects to the configured broker —
/// switching is a config change only. Both consumers (geocode-on-address-changed, invalidate-cache-on-
/// menu-updated) carry the <c>Contracts</c> integration events and depend only on Contracts/shared
/// abstractions across module lines (Step 2 rule (e) green).
/// </summary>
public static class AddMessagingExtensions
{
    /// <summary>
    /// Registers MassTransit + the two cross-cutting consumers, with the transport bound from
    /// <paramref name="configuration"/>'s <c>Messaging</c> section. The geocode consumer requires the
    /// Contracts <c>IRestaurantGeocoder</c> seam and the cache consumer requires <c>ICacheService</c>;
    /// the host wires those via the owning module's registration + <c>AddRedisCache</c>.
    /// </summary>
    public static IServiceCollection AddMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = new MessagingOptions();
        configuration.GetSection(MessagingOptions.SectionName).Bind(options);

        services.AddMassTransit(bus =>
        {
            bus.AddConsumer<GeocodeOnAddressChangedConsumer>();
            bus.AddConsumer<InvalidateCacheOnMenuUpdatedConsumer>();

            if (string.Equals(options.Transport, MessagingTransport.RabbitMq, StringComparison.OrdinalIgnoreCase))
            {
                bus.UsingRabbitMq((context, cfg) =>
                {
                    cfg.Host(options.Host, options.VirtualHost, host =>
                    {
                        host.Username(options.Username);
                        host.Password(options.Password);
                    });
                    cfg.ConfigureEndpoints(context);
                });
            }
            else
            {
                // The in-process transport: no broker, same publish/consume semantics. This is the
                // default until the RabbitMQ broker is enabled by config (invariant #4).
                bus.UsingInMemory((context, cfg) => cfg.ConfigureEndpoints(context));
            }
        });

        return services;
    }
}
