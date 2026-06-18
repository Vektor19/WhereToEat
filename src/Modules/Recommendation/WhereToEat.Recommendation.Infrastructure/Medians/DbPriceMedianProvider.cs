using Dapper;
using WhereToEat.BuildingBlocks.Persistence;
using WhereToEat.Contracts.Recommendation;
using WhereToEat.Recommendation.Application.Abstractions;

namespace WhereToEat.Recommendation.Infrastructure.Medians;

/// <summary>
/// The Step 7 <see cref="IPriceMedianProvider"/> implementation: it reads the <b>precomputed</b>
/// per-area/category median <b>directly from the DB median table</b> (created in migration 0010) —
/// <b>DB-only, no Redis</b>. The median is a ready value the nightly Step 12 job populates; this
/// provider never computes a median per request. Step 13 later adds Redis as a transparent caching
/// decorator <b>over this same port</b>, so this class and the port stay unchanged.
/// <para>
/// For a multi-item selection it sums the per-selection medians into a basket median (so the
/// market reference matches the basket the candidate prices represent), preferring the per-area row
/// and falling back to the city-wide ('*') row per selection (the threshold-N fallback). Returns
/// <c>null</c> only when no median is available for any selection (then <c>f_price</c> goes neutral).
/// </para>
/// </summary>
public sealed class DbPriceMedianProvider : IPriceMedianProvider
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public DbPriceMedianProvider(ISqlConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    /// <inheritdoc />
    public async Task<decimal?> GetMedianBasketAmountAsync(
        IReadOnlyList<SelectedItem> items,
        UserGeo? userGeo,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0)
        {
            return null;
        }

        var areaKey = AreaKey.Resolve(userGeo);

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        decimal? basketMedian = null;
        foreach (var item in items)
        {
            var sql = item.DishId is not null
                ? RecommendationSql.SelectDishMedian
                : RecommendationSql.SelectCategoryMedian;

            var amount = await connection.QuerySingleOrDefaultAsync<decimal?>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        item.DishId,
                        item.CategoryId,
                        AreaKey = areaKey,
                        CityWideAreaKey = RecommendationSql.CityWideAreaKey,
                    },
                    cancellationToken: cancellationToken))
                .ConfigureAwait(false);

            if (amount is not null)
            {
                // Accumulate into the basket median; a selection with no median simply contributes
                // nothing rather than nulling the whole basket (a partial reference still beats none).
                basketMedian = (basketMedian ?? 0m) + amount.Value;
            }
        }

        return basketMedian;
    }
}
