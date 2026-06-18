using Microsoft.Extensions.DependencyInjection;

namespace WhereToEat.Catalog.Search.Application.DependencyInjection;

/// <summary>
/// The composition entry point for the Catalog <b>Search</b> sub-module: registers the deterministic
/// prefix-search use-cases. It depends on the Catalog module's <c>ICatalogRepository</c> already being
/// registered (via <c>AddCatalogModule</c>), so the host calls <c>AddCatalogModule</c> first, then
/// <c>AddCatalogSearchModule</c>. The sub-module has no infrastructure of its own — it reuses the
/// catalog data layer through the shared port (the search queries are pure deterministic filters,
/// invariant #1).
/// </summary>
public static class AddCatalogSearchModuleExtensions
{
    /// <summary>Registers the Catalog.Search prefix-lookup query handlers (scoped).</summary>
    public static IServiceCollection AddCatalogSearchModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<SearchCategoriesQuery>();
        services.AddScoped<SearchDishesQuery>();

        return services;
    }
}
