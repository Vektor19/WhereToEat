using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;

namespace WhereToEat.BuildingBlocks.Caching;

/// <summary>
/// Wires the <see cref="ICacheService"/> abstraction a host calls from its composition root. When the
/// <c>Redis</c> section is enabled and has a connection string it connects a singleton
/// <see cref="IConnectionMultiplexer"/> and registers <see cref="RedisCacheService"/>; otherwise it
/// registers the <see cref="NullCacheService"/> so the host runs Redis-free (the read-through caches
/// degrade to their DB-only sources — Step 7/12 stay correct with caching off).
/// </summary>
public static class AddRedisCacheExtensions
{
    /// <summary>
    /// Registers <see cref="ICacheService"/> bound to the <paramref name="configuration"/>'s <c>Redis</c>
    /// section. Idempotent via <c>TryAdd</c> so multiple module registrations do not double-wire it.
    /// </summary>
    public static IServiceCollection AddRedisCache(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = new RedisCacheOptions();
        configuration.GetSection(RedisCacheOptions.SectionName).Bind(options);

        if (options.Enabled && !string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            services.TryAddSingleton<IConnectionMultiplexer>(
                _ => ConnectionMultiplexer.Connect(options.ConnectionString));
            services.TryAddSingleton<ICacheService, RedisCacheService>();
        }
        else
        {
            services.TryAddSingleton<ICacheService, NullCacheService>();
        }

        return services;
    }

    /// <summary>
    /// Registers <see cref="RedisCacheService"/> over an already-built <paramref name="multiplexer"/> —
    /// used by the integration tests that connect a Testcontainers Redis directly.
    /// </summary>
    public static IServiceCollection AddRedisCache(
        this IServiceCollection services,
        IConnectionMultiplexer multiplexer)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(multiplexer);

        services.TryAddSingleton(multiplexer);
        services.TryAddSingleton<ICacheService, RedisCacheService>();
        return services;
    }
}
