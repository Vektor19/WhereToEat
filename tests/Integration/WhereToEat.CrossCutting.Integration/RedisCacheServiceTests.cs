using FluentAssertions;
using WhereToEat.BuildingBlocks.Caching;
using Xunit;

namespace WhereToEat.CrossCutting.Integration;

/// <summary>
/// Exercises <see cref="RedisCacheService"/> against a real Redis container (Step 13): get/set/remove,
/// expiry, prefix removal (the menu-update invalidation scope), and the cross-replica slot-claim the
/// parser rate-limit uses.
/// </summary>
[Collection("redis")]
public sealed class RedisCacheServiceTests
{
    private readonly RedisCacheService _cache;

    public RedisCacheServiceTests(RedisFixture fixture) => _cache = new RedisCacheService(fixture.Multiplexer);

    [Fact]
    public async Task Set_then_Get_returns_the_value()
    {
        var key = Key();
        await _cache.SetStringAsync(key, "borsch");

        (await _cache.GetStringAsync(key)).Should().Be("borsch");
    }

    [Fact]
    public async Task Get_on_a_missing_key_returns_null()
    {
        (await _cache.GetStringAsync(Key())).Should().BeNull();
    }

    [Fact]
    public async Task Remove_deletes_the_entry()
    {
        var key = Key();
        await _cache.SetStringAsync(key, "v");

        await _cache.RemoveAsync(key);

        (await _cache.GetStringAsync(key)).Should().BeNull();
    }

    [Fact]
    public async Task Set_with_expiry_lets_the_entry_lapse()
    {
        var key = Key();
        await _cache.SetStringAsync(key, "v", TimeSpan.FromMilliseconds(150));

        (await _cache.GetStringAsync(key)).Should().Be("v");

        await Task.Delay(400);

        (await _cache.GetStringAsync(key)).Should().BeNull("the entry should have expired");
    }

    [Fact]
    public async Task RemoveByPrefix_clears_only_the_matching_namespace()
    {
        var prefix = $"median:{Guid.NewGuid():N}:";
        await _cache.SetStringAsync(prefix + "a", "1");
        await _cache.SetStringAsync(prefix + "b", "2");
        var survivor = Key();
        await _cache.SetStringAsync(survivor, "keep");

        await _cache.RemoveByPrefixAsync(prefix);

        (await _cache.GetStringAsync(prefix + "a")).Should().BeNull();
        (await _cache.GetStringAsync(prefix + "b")).Should().BeNull();
        (await _cache.GetStringAsync(survivor)).Should().Be("keep", "a key outside the prefix is untouched");
    }

    [Fact]
    public async Task TryClaimSlot_grants_once_per_interval_then_refuses()
    {
        var key = Key();

        (await _cache.TryClaimSlotAsync(key, TimeSpan.FromSeconds(30))).Should().BeTrue("the first caller claims the slot");
        (await _cache.TryClaimSlotAsync(key, TimeSpan.FromSeconds(30))).Should().BeFalse("a second caller within the interval is refused");
    }

    private static string Key() => $"test:{Guid.NewGuid():N}";
}
