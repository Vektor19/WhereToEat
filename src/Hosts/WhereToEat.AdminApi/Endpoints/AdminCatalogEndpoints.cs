using WhereToEat.Admin.Application.Addresses;
using WhereToEat.Admin.Application.MenuItems;
using WhereToEat.BuildingBlocks.Auth;

namespace WhereToEat.AdminApi.Endpoints;

/// <summary>
/// Admin catalog-edit endpoints (§4): edit a menu item's price/weight/dish (re-categorisation) and
/// edit a restaurant's address (which raises the Step 8 re-geocode flow). Both require the admin-only
/// policy — the admin is the source of truth over the parser (invariant #3).
/// </summary>
internal static class AdminCatalogEndpoints
{
    public static IEndpointRouteBuilder MapAdminCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPut("/admin/menu-items/{id:guid}", async (
                Guid id,
                EditMenuItemBody body,
                EditMenuItemCommandHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler
                    .HandleAsync(
                        new EditMenuItemCommand(id, body.DishId, body.PriceAmount, body.PriceCurrency, body.Weight),
                        ct)
                    .ConfigureAwait(false);
                return AdminResults.ToHttp(result);
            })
            .WithName("AdminEditMenuItem")
            .WithTags("AdminCatalog")
            .RequireAuthorization(AdminAuthorization.PolicyName);

        app.MapPut("/admin/restaurants/{id:guid}/address", async (
                Guid id,
                EditAddressBody body,
                EditAddressCommandHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler
                    .HandleAsync(new EditAddressCommand(id, body.AddressLine, body.City), ct)
                    .ConfigureAwait(false);
                return AdminResults.ToHttp(result);
            })
            .WithName("AdminEditAddress")
            .WithTags("AdminCatalog")
            .RequireAuthorization(AdminAuthorization.PolicyName);

        return app;
    }
}

/// <summary>The wire body for editing a menu item (price/weight + the dish for re-categorisation).</summary>
internal sealed record EditMenuItemBody(Guid DishId, decimal PriceAmount, string PriceCurrency, string? Weight);

/// <summary>The wire body for editing a restaurant's address.</summary>
internal sealed record EditAddressBody(string AddressLine, string? City);
