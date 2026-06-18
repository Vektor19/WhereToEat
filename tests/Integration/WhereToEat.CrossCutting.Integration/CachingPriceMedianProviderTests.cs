using Dapper;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using WhereToEat.BuildingBlocks.Caching;
using WhereToEat.BuildingBlocks.Persistence;
using WhereToEat.Contracts.Recommendation;
using WhereToEat.Recommendation.Infrastructure.Medians;
using Xunit;

namespace WhereToEat.CrossCutting.Integration;

/// <summary>
/// Proves the Step 13 <see cref="CachingPriceMedianProvider"/> is a <b>transparent</b> decorator over the
/// Step 7 <see cref="DbPriceMedianProvider"/>: a cache miss reads the DB median table and populates the
/// cache; a subsequent hit returns the cached value <b>without</b> touching the DB (proven by deleting
/// the DB row after the first call and still getting the same answer); and the value returned is
/// <b>identical</b> to what the DB-only provider returns — so Step 7's recommendation results are
/// unchanged whether the decorator is registered or not.
/// <para>Uses a real Redis container (the production cache) + a real SQL container (the median table).</para>
/// </summary>
[Collection("crosscutting-sql")]
public sealed class CachingPriceMedianProviderTests : IClassFixture<RedisFixture>
{
    private readonly SqlServerFixture _sql;
    private readonly RedisFixture _redis;

    public CachingPriceMedianProviderTests(SqlServerFixture sql, RedisFixture redis)
    {
        _sql = sql;
        _redis = redis;
    }

    [Fact]
    public async Task Miss_reads_db_and_populates_then_hit_skips_db_with_identical_value()
    {
        var dishId = Guid.NewGuid();
        const decimal median = 137.50m;
        await SeedCityWideDishMedianAsync(dishId, median);

        var factory = new SqlConnectionFactory(_sql.ConnectionString);
        var dbProvider = new DbPriceMedianProvider(factory);
        var cache = new RedisCacheService(_redis.Multiplexer);
        var decorator = new CachingPriceMedianProvider(dbProvider, cache);

        var items = new[] { SelectedItem.Dish(dishId) };

        // The DB-only provider's answer — the reference Step 7 result.
        var dbOnly = await dbProvider.GetMedianBasketAmountAsync(items, userGeo: null);
        dbOnly.Should().Be(median);

        // Miss → reads the DB → populates the cache. Same value as DB-only (transparent).
        var firstThroughDecorator = await decorator.GetMedianBasketAmountAsync(items, userGeo: null);
        firstThroughDecorator.Should().Be(median).And.Be(dbOnly);

        // Now remove the DB row. If the decorator still returns the value, it came from the cache (no DB).
        await DeleteDishMedianAsync(dishId);
        (await dbProvider.GetMedianBasketAmountAsync(items, userGeo: null))
            .Should().BeNull("the DB row is gone — the DB-only provider must now miss");

        var secondThroughDecorator = await decorator.GetMedianBasketAmountAsync(items, userGeo: null);
        secondThroughDecorator.Should().Be(median, "a cache hit returns the populated value without touching the DB");
    }

    private async Task SeedCityWideDishMedianAsync(Guid dishId, decimal amount)
    {
        await using var conn = new SqlConnection(_sql.ConnectionString);
        await conn.ExecuteAsync(
            "INSERT INTO recommendation.PriceMedian (AreaKey, DishId, MedianAmount, Currency, SampleSize) " +
            "VALUES ('*', @DishId, @Amount, 'UAH', 10);",
            new { DishId = dishId, Amount = amount });
    }

    private async Task DeleteDishMedianAsync(Guid dishId)
    {
        await using var conn = new SqlConnection(_sql.ConnectionString);
        await conn.ExecuteAsync(
            "DELETE FROM recommendation.PriceMedian WHERE DishId = @DishId;",
            new { DishId = dishId });
    }
}
