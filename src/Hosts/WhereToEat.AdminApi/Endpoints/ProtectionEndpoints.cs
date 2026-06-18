using WhereToEat.Admin.Application.Protection;
using WhereToEat.BuildingBlocks.Auth;

namespace WhereToEat.AdminApi.Endpoints;

/// <summary>
/// The admin &gt; parser protection endpoints (invariant #3): set the per-MenuItem <c>DoNotParse</c>
/// flag and the restaurant-level <c>DoNotUpdate</c> flag. These are the flags the Step 9 parser
/// persist gate observes so hand-curated data is never overwritten. Admin-only.
/// </summary>
internal static class ProtectionEndpoints
{
    public static IEndpointRouteBuilder MapProtectionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPut("/admin/menu-items/{id:guid}/do-not-parse", async (
                Guid id,
                FlagBody body,
                SetMenuItemDoNotParseCommandHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler
                    .HandleAsync(new SetMenuItemDoNotParseCommand(id, body.Value), ct)
                    .ConfigureAwait(false);
                return AdminResults.ToHttp(result);
            })
            .WithName("AdminSetMenuItemDoNotParse")
            .WithTags("AdminProtection")
            .RequireAuthorization(AdminAuthorization.PolicyName);

        app.MapPut("/admin/restaurants/{id:guid}/do-not-update", async (
                Guid id,
                FlagBody body,
                SetRestaurantDoNotUpdateCommandHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler
                    .HandleAsync(new SetRestaurantDoNotUpdateCommand(id, body.Value), ct)
                    .ConfigureAwait(false);
                return AdminResults.ToHttp(result);
            })
            .WithName("AdminSetRestaurantDoNotUpdate")
            .WithTags("AdminProtection")
            .RequireAuthorization(AdminAuthorization.PolicyName);

        return app;
    }
}

/// <summary>The wire body for a boolean protection flag toggle.</summary>
internal sealed record FlagBody(bool Value);
