using Microsoft.Extensions.DependencyInjection;
using WhereToEat.Catalog.Application.Queries;

namespace WhereToEat.Catalog.Infrastructure.DependencyInjection;

/// <summary>
/// The per-module composition entry point the public host calls from its composition root (Step 6):
/// it wires the Catalog read use-cases (the <c>*Query</c> handlers) together with the Dapper data
/// layer (<see cref="CatalogInfrastructureServiceCollectionExtensions.AddCatalogPersistence"/>) so a
/// host only needs <c>AddCatalogModule(connectionString)</c>. Keeping the wiring in one extension
/// per module is the modular-monolith composition convention from the design.
/// </summary>
public static class AddCatalogModuleExtensions
{
    /// <summary>
    /// Registers the Catalog module's persistence and read-query handlers against
    /// <paramref name="connectionString"/>. Query handlers are scoped (they hold a scoped repository).
    /// </summary>
    public static IServiceCollection AddCatalogModule(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddCatalogPersistence(connectionString);

        services.AddScoped<ListCategoriesQuery>();
        services.AddScoped<ListDishesByCategoryQuery>();
        services.AddScoped<GetRestaurantDetailsQuery>();
        services.AddScoped<GetRestaurantMapPayloadQuery>();

        return services;
    }
}
