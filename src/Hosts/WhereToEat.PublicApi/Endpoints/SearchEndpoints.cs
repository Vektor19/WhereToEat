using WhereToEat.Catalog.Search.Application;

namespace WhereToEat.PublicApi.Endpoints;

/// <summary>
/// The anonymous deterministic selection-building search endpoints. They take a literal
/// <c>prefix</c> + optional <c>limit</c> and run an anchored prefix filter — there is <b>no</b>
/// free-text/NLP parsing (invariant #1). Served by the Catalog.Search sub-module's query handlers.
/// </summary>
internal static class SearchEndpoints
{
    public static IEndpointRouteBuilder MapSearchEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/search/categories", async (string? prefix, int? limit, SearchCategoriesQuery query, CancellationToken ct) =>
                Results.Ok(await query.ExecuteAsync(prefix, limit ?? SearchCategoriesQuery.DefaultLimit, ct).ConfigureAwait(false)))
            .WithName("SearchCategories")
            .WithTags("Search")
            .AllowAnonymous();

        app.MapGet("/search/dishes", async (string? prefix, int? limit, SearchDishesQuery query, CancellationToken ct) =>
                Results.Ok(await query.ExecuteAsync(prefix, limit ?? SearchDishesQuery.DefaultLimit, ct).ConfigureAwait(false)))
            .WithName("SearchDishes")
            .WithTags("Search")
            .AllowAnonymous();

        return app;
    }
}
