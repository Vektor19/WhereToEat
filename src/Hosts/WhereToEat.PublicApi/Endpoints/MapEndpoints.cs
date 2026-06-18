using WhereToEat.Catalog.Application.Queries;

namespace WhereToEat.PublicApi.Endpoints;

/// <summary>
/// The "Глянути на карті" Map endpoint (§5.7). It returns a payload assembled <b>only</b> from the
/// restaurant's stored fields (our OSM-sourced coordinates + optional Place ID / deep-link), live
/// with nothing cached, and <b>no geocoder call</b> (the geocoder port arrives in Step 8). A
/// restaurant without stored coordinates gets the well-defined "no map payload" response; a missing
/// restaurant gets 404. No Google rating or Google-sourced coordinate is ever returned.
/// </summary>
internal static class MapEndpoints
{
    public static IEndpointRouteBuilder MapMapEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/map/{restaurantId:guid}", async (Guid restaurantId, GetRestaurantMapPayloadQuery query, CancellationToken ct) =>
            {
                var payload = await query.ExecuteAsync(restaurantId, ct).ConfigureAwait(false);
                return payload is null ? Results.NotFound() : Results.Ok(payload);
            })
            .WithName("GetRestaurantMapPayload")
            .WithTags("Map")
            .AllowAnonymous();

        return app;
    }
}
