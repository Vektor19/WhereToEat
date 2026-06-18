using WhereToEat.BuildingBlocks.Auth;
using WhereToEat.Monetization.Application.Ads;
using WhereToEat.Monetization.Application.Verified;
using WhereToEat.Monetization.Domain;

namespace WhereToEat.AdminApi.Endpoints;

/// <summary>
/// The admin-side Monetization endpoints (Step 14, future-facing — no real billing): grant/revoke a
/// venue's Verified status and create a <b>labeled</b> ad placement. Granting Verified flips the Step 5
/// real-photo permission gate + sets the tier with NO effect on organic ranking (invariant #10) or the
/// always-free contact links (§5.8); an ad placement is always a separate, marked slot the Step 7
/// pipeline never uses as a rank modifier (a guard test pins that the Recommendation module does not
/// reference Monetization at all). Payment runs strictly through the no-op <c>IPaymentGateway</c> seam
/// inside the handlers. All endpoints require the SAME admin-only policy as the other admin endpoints.
/// </summary>
internal static class MonetizationEndpoints
{
    public static IEndpointRouteBuilder MapMonetizationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPut("/admin/venues/{id:guid}/verified", async (
                Guid id,
                GrantVerifiedBody body,
                GrantVerifiedCommandHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler
                    .HandleAsync(new GrantVerifiedCommand(id, body.Tier), ct)
                    .ConfigureAwait(false);
                return AdminResults.ToHttp(result);
            })
            .WithName("AdminGrantVerified")
            .WithTags("AdminMonetization")
            .RequireAuthorization(AdminAuthorization.PolicyName);

        app.MapDelete("/admin/venues/{id:guid}/verified", async (
                Guid id,
                RevokeVerifiedCommandHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler
                    .HandleAsync(new RevokeVerifiedCommand(id), ct)
                    .ConfigureAwait(false);
                return AdminResults.ToHttp(result);
            })
            .WithName("AdminRevokeVerified")
            .WithTags("AdminMonetization")
            .RequireAuthorization(AdminAuthorization.PolicyName);

        app.MapPost("/admin/venues/{id:guid}/ad-placements", async (
                Guid id,
                CreateAdPlacementBody body,
                CreateAdPlacementCommandHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler
                    .HandleAsync(
                        new CreateAdPlacementCommand(id, body.TargetingKey, body.StartsAt, body.EndsAt),
                        ct)
                    .ConfigureAwait(false);

                return result.IsSuccess
                    ? Results.Created($"/admin/ad-placements/{result.Value}", new { id = result.Value })
                    : AdminResults.ToHttp(WhereToEat.SharedKernel.Results.Result.Failure(result.Error));
            })
            .WithName("AdminCreateAdPlacement")
            .WithTags("AdminMonetization")
            .RequireAuthorization(AdminAuthorization.PolicyName);

        return app;
    }
}

/// <summary>The wire body for granting Verified: the paid tier to grant (Basic or Pro).</summary>
internal sealed record GrantVerifiedBody(SubscriptionTier Tier);

/// <summary>The wire body for creating a labeled ad placement (targeting + active window).</summary>
internal sealed record CreateAdPlacementBody(string TargetingKey, DateTimeOffset StartsAt, DateTimeOffset EndsAt);
