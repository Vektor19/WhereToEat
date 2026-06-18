using System.Data;
using Dapper;
using WhereToEat.BuildingBlocks.Persistence;
using WhereToEat.Catalog.Application.Abstractions;
using WhereToEat.Catalog.Domain.Abstractions;
using WhereToEat.Catalog.Domain.Identifiers;
using WhereToEat.Catalog.Domain.Restaurants;
using WhereToEat.Catalog.Domain.Taxonomy;
using WhereToEat.Catalog.Infrastructure.Persistence.Mappings;
using WhereToEat.Catalog.Infrastructure.Persistence.Sql;

namespace WhereToEat.Catalog.Infrastructure.Persistence;

/// <summary>
/// The Dapper-backed catalog adapter: hand-written SQL (see <see cref="CatalogSql"/>) over the
/// SQL-script-owned catalog schema, mapping rows to/from the domain aggregates via
/// <see cref="RowMapper"/>. It implements <b>both</b> catalog ports — the write-side
/// <see cref="ICatalogRepository"/> (load/save whole <see cref="Restaurant"/> aggregates +
/// by-id loads) and the query-side <see cref="ICatalogReadPort"/> (taxonomy list + anchored
/// prefix-search reads) — from one place, since the same connection/SQL serve both. The
/// aggregate boundary is the unit of write persistence, exactly as the write port promises. No
/// EF Core — Dapper only (invariant: EF is Admin.Infrastructure-only).
/// </summary>
public sealed class DapperCatalogRepository : ICatalogRepository, ICatalogReadPort
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public DapperCatalogRepository(ISqlConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    /// <inheritdoc />
    public async Task<Restaurant?> GetRestaurantByIdAsync(RestaurantId id, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        var restaurantRow = await connection.QuerySingleOrDefaultAsync<RestaurantRow>(
            new CommandDefinition(CatalogSql.SelectRestaurantById, new { Id = id.Value }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        if (restaurantRow is null)
        {
            return null;
        }

        var menuItemRows = await connection.QueryAsync<MenuItemRow>(
            new CommandDefinition(
                CatalogSql.SelectMenuItemsByRestaurant,
                new { RestaurantId = id.Value },
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        var contactLinkRows = await connection.QueryAsync<ContactLinkRow>(
            new CommandDefinition(
                CatalogSql.SelectContactLinksByRestaurant,
                new { RestaurantId = id.Value },
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return RowMapper.ToRestaurant(restaurantRow, menuItemRows, contactLinkRows);
    }

    /// <inheritdoc />
    public async Task<Category?> GetCategoryByIdAsync(CategoryId id, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        var row = await connection.QuerySingleOrDefaultAsync<CategoryRow>(
            new CommandDefinition(CatalogSql.SelectCategoryById, new { Id = id.Value }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return row is null ? null : RowMapper.ToCategory(row);
    }

    /// <inheritdoc />
    public async Task<Dish?> GetDishByIdAsync(DishId id, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        var row = await connection.QuerySingleOrDefaultAsync<DishRow>(
            new CommandDefinition(CatalogSql.SelectDishById, new { Id = id.Value }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return row is null ? null : RowMapper.ToDish(row);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Category>> ListCategoriesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        var rows = await connection.QueryAsync<CategoryRow>(
            new CommandDefinition(CatalogSql.SelectAllCategories, cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return rows.Select(RowMapper.ToCategory).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Dish>> ListDishesByCategoryAsync(CategoryId categoryId, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        var rows = await connection.QueryAsync<DishRow>(
            new CommandDefinition(
                CatalogSql.SelectDishesByCategory,
                new { CategoryId = categoryId.Value },
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return rows.Select(RowMapper.ToDish).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Category>> SearchCategoriesByPrefixAsync(string prefix, int limit, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        var rows = await connection.QueryAsync<CategoryRow>(
            new CommandDefinition(
                CatalogSql.SearchCategoriesByPrefix,
                new { Prefix = EscapeLikePrefix(prefix), Limit = limit },
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return rows.Select(RowMapper.ToCategory).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Dish>> SearchDishesByPrefixAsync(string prefix, int limit, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        var rows = await connection.QueryAsync<DishRow>(
            new CommandDefinition(
                CatalogSql.SearchDishesByPrefix,
                new { Prefix = EscapeLikePrefix(prefix), Limit = limit },
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return rows.Select(RowMapper.ToDish).ToList();
    }

    /// <inheritdoc />
    public async Task AddRestaurantAsync(Restaurant restaurant, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(restaurant);

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        // The header + all children are one aggregate, so they persist atomically: if a child
        // INSERT fails, the header (and any earlier children) roll back rather than leaving a
        // half-written aggregate.
        using var transaction = connection.BeginTransaction();
        try
        {
            await InsertRestaurantHeaderAsync(connection, transaction, restaurant, cancellationToken).ConfigureAwait(false);
            await InsertChildrenAsync(connection, transaction, restaurant, cancellationToken).ConfigureAwait(false);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task UpdateRestaurantAsync(Restaurant restaurant, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(restaurant);

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        // The whole UPDATE + replace-all of children must be atomic: deleting the old children and
        // then failing to insert the new ones would otherwise leave the restaurant permanently
        // stripped of its menu/contact links with no rollback. Run it all in one transaction.
        using var transaction = connection.BeginTransaction();
        try
        {
            var updated = await connection.ExecuteAsync(
                new CommandDefinition(
                    CatalogSql.UpdateRestaurant,
                    RestaurantHeaderParameters(restaurant),
                    transaction,
                    cancellationToken: cancellationToken))
                .ConfigureAwait(false);

            if (updated == 0)
            {
                throw new InvalidOperationException(
                    $"Cannot update restaurant '{restaurant.Id}' — no such row exists. Use AddRestaurantAsync for new aggregates.");
            }

            // Children are owned by the aggregate; replace-all keeps the stored set in lock-step with
            // the in-memory aggregate without diffing (the catalog write path is low-frequency).
            await connection.ExecuteAsync(
                new CommandDefinition(
                    CatalogSql.DeleteMenuItemsByRestaurant,
                    new { RestaurantId = restaurant.Id.Value },
                    transaction,
                    cancellationToken: cancellationToken))
                .ConfigureAwait(false);

            await connection.ExecuteAsync(
                new CommandDefinition(
                    CatalogSql.DeleteContactLinksByRestaurant,
                    new { RestaurantId = restaurant.Id.Value },
                    transaction,
                    cancellationToken: cancellationToken))
                .ConfigureAwait(false);

            await InsertChildrenAsync(connection, transaction, restaurant, cancellationToken).ConfigureAwait(false);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task AddCategoryAsync(Category category, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(category);

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        await connection.ExecuteAsync(
            new CommandDefinition(
                CatalogSql.InsertCategory,
                new { Id = category.Id.Value, category.Name },
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task AddDishAsync(Dish dish, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dish);

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        await connection.ExecuteAsync(
            new CommandDefinition(
                CatalogSql.InsertDish,
                new { Id = dish.Id.Value, CategoryId = dish.CategoryId.Value, dish.CanonicalName },
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Escapes the LIKE wildcards (<c>% _ [ \</c>) in a user-supplied prefix with the <c>\</c>
    /// escape char the prefix queries declare (<c>ESCAPE N'\'</c>). This keeps the filter a literal
    /// <b>prefix match</b> — a user typing "50%" looks for names starting with "50%", not "starts
    /// with 50 then anything" — so the search stays deterministic and predictable (invariant #1).
    /// </summary>
    private static string EscapeLikePrefix(string prefix)
        => (prefix ?? string.Empty)
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal)
            .Replace("[", "\\[", StringComparison.Ordinal);

    private static async Task InsertRestaurantHeaderAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        Restaurant restaurant,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(
            new CommandDefinition(
                CatalogSql.InsertRestaurant,
                RestaurantHeaderParameters(restaurant),
                transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    private static async Task InsertChildrenAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        Restaurant restaurant,
        CancellationToken cancellationToken)
    {
        foreach (var item in restaurant.MenuItems)
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    CatalogSql.InsertMenuItem,
                    new
                    {
                        Id = item.Id.Value,
                        RestaurantId = restaurant.Id.Value,
                        DishId = item.DishId.Value,
                        PriceAmount = item.Price.Amount,
                        PriceCurrency = item.Price.Currency,
                        item.Weight,
                        Source = (int)item.Source,
                        item.DoNotParse,
                    },
                    transaction,
                    cancellationToken: cancellationToken))
                .ConfigureAwait(false);
        }

        foreach (var link in restaurant.ContactLinks)
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    CatalogSql.InsertContactLink,
                    new
                    {
                        RestaurantId = restaurant.Id.Value,
                        Kind = (int)link.Kind,
                        link.Url,
                        link.Label,
                    },
                    transaction,
                    cancellationToken: cancellationToken))
                .ConfigureAwait(false);
        }
    }

    private static object RestaurantHeaderParameters(Restaurant restaurant) => new
    {
        Id = restaurant.Id.Value,
        restaurant.Name,
        AddressLine = restaurant.Address.Line,
        AddressCity = restaurant.Address.City,
        Wkt = RowMapper.ToPointWkt(restaurant.Coordinates),
        PlaceId = restaurant.Coordinates?.PlaceId,
        MapsDeepLink = restaurant.Coordinates?.MapsDeepLink,
    };
}
