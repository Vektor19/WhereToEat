using StackExchange.Redis;
using Testcontainers.Redis;
using Xunit;

namespace WhereToEat.CrossCutting.Integration;

/// <summary>
/// A Redis Testcontainers fixture: starts one Redis container, exposes a connected
/// <see cref="IConnectionMultiplexer"/>, and tears it down afterwards. Shared by the cache tests so the
/// container start cost is paid once.
/// </summary>
public sealed class RedisFixture : IAsyncLifetime
{
    private readonly RedisContainer _container = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    private ConnectionMultiplexer? _multiplexer;

    /// <summary>The connected multiplexer against the container.</summary>
    public IConnectionMultiplexer Multiplexer =>
        _multiplexer ?? throw new InvalidOperationException("The Redis fixture has not been initialized.");

    public async Task InitializeAsync()
    {
        await _container.StartAsync().ConfigureAwait(false);
        _multiplexer = await ConnectionMultiplexer.ConnectAsync(_container.GetConnectionString()).ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        if (_multiplexer is not null)
        {
            await _multiplexer.DisposeAsync().ConfigureAwait(false);
        }

        await _container.DisposeAsync().ConfigureAwait(false);
    }
}

/// <summary>Shares one Redis container across the cache test classes.</summary>
[CollectionDefinition("redis")]
public sealed class RedisCollectionDefinition : ICollectionFixture<RedisFixture>;
