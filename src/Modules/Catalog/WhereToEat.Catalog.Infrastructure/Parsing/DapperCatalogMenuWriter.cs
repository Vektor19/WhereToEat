using System.Data;
using Dapper;
using WhereToEat.BuildingBlocks.Persistence;
using WhereToEat.Contracts.Parsing;

namespace WhereToEat.Catalog.Infrastructure.Parsing;

/// <summary>
/// The production Catalog-side adapter for the Parsing module's <see cref="ICatalogMenuWriter"/> seam
/// (Step 9 / Step 13). It reads the admin-protection context the parser persist gate consults
/// (invariant #3, admin &gt; parser) directly from the script-created tables —
/// <c>admin.RestaurantProtection.DoNotUpdate</c> for the venue-level flag and
/// <c>catalog.MenuItem.DoNotParse</c> for the per-item flags — and upserts the cleared items the gate
/// already filtered. Restaurants are matched by name + address line (the parser's natural key, exactly
/// as the Contracts seam specifies).
/// <para>
/// The persist <i>gate</i> itself lives in the Parsing handler (it reads the context, skips a
/// <c>DoNotUpdate</c> venue entirely, and excludes <c>DoNotParse</c> dishes); this writer only performs
/// the upsert it is handed. Living inside the Catalog module keeps the coupling here — Parsing depends
/// only on the <c>Contracts</c> port (Step 2 module-isolation rule (d) stays green).
/// </para>
/// </summary>
public sealed class DapperCatalogMenuWriter : ICatalogMenuWriter
{
    // catalog.MenuItem.Source value for parser provenance (0 = Parsed draft; admin edits use a higher
    // value, so the parser write never masquerades as an admin-verified one).
    private const int ParsedSource = 0;

    private readonly ISqlConnectionFactory _connectionFactory;

    public DapperCatalogMenuWriter(ISqlConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    /// <inheritdoc />
    public async Task<RestaurantProtectionContextDto> GetProtectionContextAsync(
        string restaurantName,
        string addressLine,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(restaurantName);
        ArgumentNullException.ThrowIfNull(addressLine);

        using var connection = await _connectionFactory
            .CreateOpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        var restaurantId = await connection.QuerySingleOrDefaultAsync<Guid?>(
                new CommandDefinition(
                    "SELECT Id FROM catalog.Restaurant WHERE Name = @Name AND AddressLine = @AddressLine;",
                    new { Name = restaurantName, AddressLine = addressLine },
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        if (restaurantId is null)
        {
            return new RestaurantProtectionContextDto(null, DoNotUpdate: false, new HashSet<Guid>());
        }

        var doNotUpdate = await connection.QuerySingleOrDefaultAsync<bool?>(
                new CommandDefinition(
                    "SELECT DoNotUpdate FROM admin.RestaurantProtection WHERE RestaurantId = @Id;",
                    new { Id = restaurantId.Value },
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false) ?? false;

        var doNotParseDishIds = (await connection.QueryAsync<Guid>(
                new CommandDefinition(
                    "SELECT DishId FROM catalog.MenuItem WHERE RestaurantId = @Id AND DoNotParse = 1;",
                    new { Id = restaurantId.Value },
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false)).ToHashSet();

        return new RestaurantProtectionContextDto(restaurantId.Value, doNotUpdate, doNotParseDishIds);
    }

    /// <inheritdoc />
    public async Task<Guid> PersistAsync(
        ParsedRestaurantFactsDto restaurant,
        IReadOnlyList<ResolvedMenuItemDto> resolvedItems,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(restaurant);
        ArgumentNullException.ThrowIfNull(resolvedItems);

        using var connection = await _connectionFactory
            .CreateOpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        using var transaction = connection.BeginTransaction();

        var restaurantId = await connection.QuerySingleOrDefaultAsync<Guid?>(
                new CommandDefinition(
                    "SELECT Id FROM catalog.Restaurant WHERE Name = @Name AND AddressLine = @AddressLine;",
                    new { restaurant.Name, restaurant.AddressLine },
                    transaction,
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        if (restaurantId is null)
        {
            restaurantId = Guid.NewGuid();
            await connection.ExecuteAsync(
                new CommandDefinition(
                    "INSERT INTO catalog.Restaurant (Id, Name, AddressLine, AddressCity) " +
                    "VALUES (@Id, @Name, @AddressLine, @City);",
                    new { Id = restaurantId.Value, restaurant.Name, restaurant.AddressLine, restaurant.City },
                    transaction,
                    cancellationToken: cancellationToken))
                .ConfigureAwait(false);

            await PersistContactLinksAsync(connection, transaction, restaurantId.Value, restaurant, cancellationToken)
                .ConfigureAwait(false);
        }

        foreach (var item in resolvedItems)
        {
            await UpsertMenuItemAsync(connection, transaction, restaurantId.Value, item, cancellationToken)
                .ConfigureAwait(false);
        }

        transaction.Commit();
        return restaurantId.Value;
    }

    private static async Task UpsertMenuItemAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        Guid restaurantId,
        ResolvedMenuItemDto item,
        CancellationToken cancellationToken)
    {
        var existingId = await connection.QuerySingleOrDefaultAsync<Guid?>(
                new CommandDefinition(
                    "SELECT Id FROM catalog.MenuItem WHERE RestaurantId = @R AND DishId = @D;",
                    new { R = restaurantId, D = item.DishId },
                    transaction,
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        if (existingId is null)
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    "INSERT INTO catalog.MenuItem (Id, RestaurantId, DishId, PriceAmount, PriceCurrency, Weight, Source, DoNotParse) " +
                    "VALUES (@Id, @R, @D, @Amount, @Currency, @Weight, @Source, 0);",
                    new
                    {
                        Id = Guid.NewGuid(),
                        R = restaurantId,
                        D = item.DishId,
                        Amount = item.PriceAmount,
                        Currency = item.PriceCurrency,
                        item.Weight,
                        Source = ParsedSource,
                    },
                    transaction,
                    cancellationToken: cancellationToken))
                .ConfigureAwait(false);
        }
        else
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    "UPDATE catalog.MenuItem SET PriceAmount = @Amount, PriceCurrency = @Currency, " +
                    "Weight = @Weight, Source = @Source WHERE Id = @Id;",
                    new
                    {
                        Id = existingId.Value,
                        Amount = item.PriceAmount,
                        Currency = item.PriceCurrency,
                        item.Weight,
                        Source = ParsedSource,
                    },
                    transaction,
                    cancellationToken: cancellationToken))
                .ConfigureAwait(false);
        }
    }

    private static async Task PersistContactLinksAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        Guid restaurantId,
        ParsedRestaurantFactsDto restaurant,
        CancellationToken cancellationToken)
    {
        // Site/social links are always-shown/never-monetized (invariant #10); we persist them for a new
        // restaurant so the details page can show them for free. Existing venues keep their (possibly
        // admin-curated) links untouched here.
        foreach (var link in restaurant.ContactLinks)
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    "INSERT INTO catalog.ContactLink (RestaurantId, Kind, Url, Label) " +
                    "VALUES (@RestaurantId, @Kind, @Url, @Label);",
                    new { RestaurantId = restaurantId, link.Kind, link.Url, link.Label },
                    transaction,
                    cancellationToken: cancellationToken))
                .ConfigureAwait(false);
        }
    }
}
