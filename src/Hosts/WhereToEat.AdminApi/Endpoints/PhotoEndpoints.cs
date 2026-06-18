using WhereToEat.Admin.Application.Photos;
using WhereToEat.BuildingBlocks.Auth;

namespace WhereToEat.AdminApi.Endpoints;

/// <summary>
/// Real-photo management for permission-granted (Verified) venues (invariant #8): toggling a real
/// photo's permission gate. Our own generic category photos are the default on every dish and are
/// never affected. Admin-only.
/// </summary>
internal static class PhotoEndpoints
{
    public static IEndpointRouteBuilder MapPhotoEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPut("/admin/photos/{id:guid}/permission", async (
                Guid id,
                FlagBody body,
                ManageRealPhotoCommandHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler
                    .HandleAsync(new ManageRealPhotoCommand(id, body.Value), ct)
                    .ConfigureAwait(false);
                return AdminResults.ToHttp(result);
            })
            .WithName("AdminManageRealPhoto")
            .WithTags("AdminPhotos")
            .RequireAuthorization(AdminAuthorization.PolicyName);

        return app;
    }
}
