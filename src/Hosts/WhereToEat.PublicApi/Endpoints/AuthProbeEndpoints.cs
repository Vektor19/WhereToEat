using WhereToEat.BuildingBlocks.Auth;

namespace WhereToEat.PublicApi.Endpoints;

/// <summary>
/// A minimal <b>authenticated</b> probe endpoint that exercises the identity seam: it requires a
/// valid token (the <see cref="AuthServiceCollectionExtensions.UserPolicy"/>) and echoes the
/// caller's mapped subject/roles from <see cref="IIdentityContext"/>. The Step 6 read endpoints are
/// anonymous; this is the placeholder that proves the auth seam (401 when unauthenticated) ahead of
/// the real authenticated endpoints (ratings) in a later step.
/// </summary>
internal static class AuthProbeEndpoints
{
    public static IEndpointRouteBuilder MapAuthProbeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/me", (IIdentityContext identity) =>
                Results.Ok(new
                {
                    identity.SubjectId,
                    Roles = identity.Roles.Select(r => r.ToString()).ToArray(),
                }))
            .WithName("Me")
            .WithTags("Auth")
            .RequireAuthorization(AuthServiceCollectionExtensions.UserPolicy);

        return app;
    }
}
