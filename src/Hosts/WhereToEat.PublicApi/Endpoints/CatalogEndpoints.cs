using WhereToEat.Catalog.Application.Queries;

namespace WhereToEat.PublicApi.Endpoints;

/// <summary>
/// The anonymous catalog read endpoints: list categories, list dishes in a category, and full
/// restaurant details. Details <b>always</b> include the contact links (§5.8). These map directly
/// onto the Catalog.Application query handlers — the host is thin orchestration only.
/// </summary>
internal static class CatalogEndpoints
{
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        // The taxonomy lists the user picks from (invariant #1: selection from lists, no NLP).
        app.MapGet("/categories", async (ListCategoriesQuery query, CancellationToken ct) =>
                Results.Ok(await query.ExecuteAsync(ct).ConfigureAwait(false)))
            .WithName("ListCategories")
            .WithTags("Catalog")
            .AllowAnonymous();

        app.MapGet("/categories/{id:guid}/dishes", async (Guid id, ListDishesByCategoryQuery query, CancellationToken ct) =>
                Results.Ok(await query.ExecuteAsync(id, ct).ConfigureAwait(false)))
            .WithName("ListDishesByCategory")
            .WithTags("Catalog")
            .AllowAnonymous();

        app.MapGet("/restaurants/{id:guid}", async (Guid id, GetRestaurantDetailsQuery query, CancellationToken ct) =>
            {
                var details = await query.ExecuteAsync(id, ct).ConfigureAwait(false);
                return details is null ? Results.NotFound() : Results.Ok(details);
            })
            .WithName("GetRestaurantDetails")
            .WithTags("Catalog")
            .AllowAnonymous();

        return app;
    }
}
