using System.Security.Cryptography;
using System.Text;
using WhereToEat.BuildingBlocks.Auth;
using WhereToEat.Ratings.Application.Submit;
using WhereToEat.SharedKernel.Results;

namespace WhereToEat.PublicApi.Endpoints;

/// <summary>
/// The <b>authenticated</b> <c>POST /restaurants/{restaurantId}/ratings</c> endpoint (§5.6 / invariant
/// #6). A signed-in user submits (or revises) their 1..5 score for a restaurant: the caller's IdP
/// <c>sub</c> from <see cref="IIdentityContext.SubjectId"/> is mapped to an <b>opaque, non-reversible</b>
/// user Guid (no PII stored — invariant #11), the path <c>restaurantId</c> and the body <c>score</c>
/// drive the Step 21 <see cref="SubmitRatingCommandHandler"/> (revise-or-create → persist → publish
/// <c>RatingGiven</c> so the recompute rebuilds the cumulative smoothed aggregate). Anonymous callers get
/// 401 (the <see cref="AuthServiceCollectionExtensions.UserPolicy"/>); an out-of-range score maps to a
/// descriptive 400 <c>{ error, message }</c> (never a 500); the happy path returns 204 No Content.
/// </summary>
internal static class RatingEndpoints
{
    public static IEndpointRouteBuilder MapRatingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/restaurants/{restaurantId:guid}/ratings",
                async (Guid restaurantId, SubmitRatingRequestBody body, IIdentityContext identity, SubmitRatingCommandHandler handler, CancellationToken ct) =>
                {
                    // The UserPolicy guards the route, so an authenticated request always carries a subject.
                    // Defensive: a token with no `sub` is treated as unauthenticated rather than a 500.
                    if (string.IsNullOrWhiteSpace(identity.SubjectId))
                    {
                        return Results.Unauthorized();
                    }

                    // Map the IdP `sub` -> an opaque, deterministic, non-reversible user Guid. The raw `sub`
                    // is never persisted (invariant #11): the rating row carries only this derived ref, and
                    // the same user maps to the same ref so a revise updates their one row (UQ constraint).
                    var userId = OpaqueUserId.FromSubject(identity.SubjectId);

                    var result = await handler
                        .HandleAsync(new SubmitRatingCommand(restaurantId, userId, body.Score), ct)
                        .ConfigureAwait(false);

                    return result.IsSuccess
                        ? Results.NoContent()
                        : Results.BadRequest(new { error = result.Error.Code, message = result.Error.Message });
                })
            .WithName("SubmitRating")
            .WithTags("Ratings")
            .RequireAuthorization(AuthServiceCollectionExtensions.UserPolicy);

        return app;
    }
}

/// <summary>The wire body for a rating submission: the 1..5 score (validated by the domain).</summary>
internal sealed record SubmitRatingRequestBody(int Score);

/// <summary>
/// Maps an external IdP subject (<c>sub</c>) to a stable, opaque user <see cref="Guid"/> with a one-way
/// SHA-256 derivation. The mapping is deterministic (the same <c>sub</c> always yields the same Guid, so a
/// revise hits the user's single <c>ratings.Rating</c> row) and non-reversible (only the derived Guid is
/// stored — the raw subject is never persisted, invariant #11). This is a stable pseudonymisation, not a
/// security secret, so a plain digest is sufficient; it is folded to 16 bytes for the Guid.
/// </summary>
internal static class OpaqueUserId
{
    public static Guid FromSubject(string subject)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(subject));

        // Take the first 16 bytes of the 32-byte digest as the Guid value (stable across calls).
        Span<byte> guidBytes = stackalloc byte[16];
        digest.AsSpan(0, 16).CopyTo(guidBytes);

        return new Guid(guidBytes);
    }
}
