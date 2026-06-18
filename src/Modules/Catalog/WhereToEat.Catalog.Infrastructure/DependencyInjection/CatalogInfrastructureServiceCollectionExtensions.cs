using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WhereToEat.BuildingBlocks.Persistence;
using WhereToEat.Catalog.Application.Abstractions;
using WhereToEat.Catalog.Domain.Abstractions;
using WhereToEat.Catalog.Infrastructure.Parsing;
using WhereToEat.Catalog.Infrastructure.Persistence;
using WhereToEat.Contracts.Geo;
using WhereToEat.Contracts.Parsing;

namespace WhereToEat.Catalog.Infrastructure.DependencyInjection;

/// <summary>
/// Registers the Catalog module's Dapper data layer: the shared SQL connection factory, the
/// <see cref="ICatalogRepository"/> domain port (implemented by <see cref="DapperCatalogRepository"/>),
/// and the geography radius pre-filter (<see cref="ICatalogSpatialReader"/>). Hosts call this from
/// their composition root (the fuller <c>AddCatalogModule</c> wiring arrives in Step 6); kept here
/// so the data layer is independently registrable and integration-testable now.
/// </summary>
public static class CatalogInfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Adds the catalog persistence services bound to <paramref name="connectionString"/>. The
    /// connection factory is a singleton (it only holds the connection string and hands out pooled
    /// connections); the repositories are scoped to a unit of work.
    /// </summary>
    public static IServiceCollection AddCatalogPersistence(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("A SQL connection string is required.", nameof(connectionString));
        }

        // The connection factory is also registered by the recommendation module; TryAdd keeps a
        // single factory when a host wires both modules, otherwise registers ours.
        services.TryAddSingleton<ISqlConnectionFactory>(_ => new SqlConnectionFactory(connectionString));

        // One Dapper adapter implements both catalog ports. Register the concrete type once (scoped
        // to a unit of work) and resolve each port from that same instance, so the write-side and
        // the query-side share a connection scope without duplicate adapters.
        services.AddScoped<DapperCatalogRepository>();
        services.AddScoped<ICatalogRepository>(sp => sp.GetRequiredService<DapperCatalogRepository>());
        services.AddScoped<ICatalogReadPort>(sp => sp.GetRequiredService<DapperCatalogRepository>());

        // ICatalogSpatialReader is internal to this assembly (it stays a module-private read model,
        // not a cross-module port). The registration lives here, inside the same assembly, so the
        // internal interface/implementation are visible to it.
        services.AddScoped<ICatalogSpatialReader, DapperCatalogSpatialReader>();

        // The Geo module's re-geocode seam (Step 8): Geo writes coordinates back through this
        // Contracts port; the adapter here goes through ICatalogRepository so Geo never touches
        // Catalog internals (the module-isolation boundary lives inside this module).
        services.AddScoped<ICatalogCoordinateWriter, CatalogCoordinateWriter>();

        // The Parsing module's catalog seams (Step 9, production-wired in Step 13): the dish resolver
        // (raw name → canonical Dish, deterministic) and the menu writer (admin-protection context +
        // upsert of cleared items). Both live inside the Catalog module so Parsing depends only on the
        // Contracts ports — the Worker/Admin hosts now resolve real adapters (WeeklyParseJob no longer
        // throws at first fire). TryAdd so a test that supplies its own double still wins.
        services.TryAddScoped<ICatalogDishResolver, DapperCatalogDishResolver>();
        services.TryAddScoped<ICatalogMenuWriter, DapperCatalogMenuWriter>();

        return services;
    }
}
