using WhereToEat.Contracts.Recommendation;
using WhereToEat.Recommendation.Application;
using WhereToEat.SharedKernel.Results;
using WhereToEat.SharedKernel.ValueObjects;

namespace WhereToEat.PublicApi.Endpoints;

/// <summary>
/// The <c>POST /recommend</c> endpoint (CLAUDE.md §6). It takes the deterministic request
/// <c>{ items, match, sort, filters, userGeo }</c> (selections from lists — invariant #1, no NLP),
/// runs the pluggable 5-step pipeline via the <see cref="RecommendQuery"/> handler, and returns the
/// ordered, filtered result. Malformed input is rejected with a descriptive 400 <b>before</b> the
/// pipeline runs: an item with neither/both ids, an out-of-range <c>userGeo</c>, an empty selection,
/// or an unknown match/sort/filter key all map to 400 (never a 500); the happy path returns 200 with
/// the ranked list (explicit sort dominates, coverage only a tie-breaker — invariant #5). Anonymous:
/// recommendation is a public read.
/// </summary>
internal static class RecommendEndpoints
{
    public static IEndpointRouteBuilder MapRecommendEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/recommend", async (RecommendRequestBody body, RecommendQuery query, CancellationToken ct) =>
            {
                // Validate the wire body into a contract first: a null/null (or both-set) item and an
                // out-of-range userGeo are user errors -> a descriptive 400, not an unhandled 500.
                var contract = body.TryToContract();
                if (contract.IsFailure)
                {
                    return Results.BadRequest(new { error = contract.Error.Code, message = contract.Error.Message });
                }

                var result = await query.ExecuteAsync(contract.Value, ct).ConfigureAwait(false);

                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : Results.BadRequest(new { error = result.Error.Code, message = result.Error.Message });
            })
            .WithName("Recommend")
            .WithTags("Recommend")
            .AllowAnonymous();

        return app;
    }
}

/// <summary>
/// The wire-friendly request body for <c>POST /recommend</c>. It mirrors the contract but uses
/// plain, JSON-deserializable item shapes (the contract's <see cref="SelectedItem"/> has a private
/// constructor, so it cannot bind directly); <see cref="TryToContract"/> validates "exactly one of
/// category/dish" per item and the <c>userGeo</c> coordinate range, returning a validation
/// <see cref="Result{T}"/> rather than throwing. Defaults keep an absent match/sort well-defined.
/// </summary>
internal sealed record RecommendRequestBody(
    IReadOnlyList<SelectedItemBody>? Items,
    string? Match,
    string? Sort,
    IReadOnlyList<FilterSelectionBody>? Filters,
    UserGeoBody? UserGeo)
{
    /// <summary>
    /// Validates and converts the body to the public contract. Returns a validation failure (mapped
    /// to 400 by the endpoint) when any item has neither or both ids set, or when <c>userGeo</c> is
    /// out of range — so neither a <see cref="SelectedItem"/> factory nor distance ranking can throw.
    /// </summary>
    public Result<RecommendationRequest> TryToContract()
    {
        var items = new List<SelectedItem>();
        foreach (var item in Items ?? Array.Empty<SelectedItemBody>())
        {
            var hasCategory = item.CategoryId is { } c && c != Guid.Empty;
            var hasDish = item.DishId is { } d && d != Guid.Empty;

            if (hasCategory == hasDish)
            {
                // Both null/empty, or both set: the two-level taxonomy requires exactly one (#2).
                return Result.Failure<RecommendationRequest>(Error.Validation(
                    "Recommend.InvalidItem",
                    "Each selected item must specify exactly one of categoryId or dishId."));
            }

            items.Add(hasDish ? SelectedItem.Dish(item.DishId!.Value) : SelectedItem.Category(item.CategoryId!.Value));
        }

        var filters = (Filters ?? Array.Empty<FilterSelectionBody>())
            .Select(f => new FilterSelection(f.Key, f.Value))
            .ToList();

        UserGeo? geo = null;
        if (UserGeo is { } g)
        {
            // Reuse the SharedKernel coordinate-range validation; an out-of-range location is a 400,
            // not a silent drop of distance ranking (the caller asked for it).
            var point = GeoPoint.Create(g.Latitude, g.Longitude);
            if (point.IsFailure)
            {
                return Result.Failure<RecommendationRequest>(Error.Validation(
                    "Recommend.InvalidUserGeo",
                    "userGeo latitude must be within [-90, 90] and longitude within [-180, 180]."));
            }

            geo = new UserGeo(g.Latitude, g.Longitude);
        }

        return Result.Success(
            new RecommendationRequest(items, Match ?? string.Empty, Sort ?? string.Empty, filters, geo));
    }
}

/// <summary>A wire selection: exactly one of <see cref="CategoryId"/>/<see cref="DishId"/> set.</summary>
internal sealed record SelectedItemBody(Guid? CategoryId, Guid? DishId);

/// <summary>A wire filter selection (key + optional raw value).</summary>
internal sealed record FilterSelectionBody(string Key, string? Value);

/// <summary>The wire user location for distance ranking.</summary>
internal sealed record UserGeoBody(double Latitude, double Longitude);
