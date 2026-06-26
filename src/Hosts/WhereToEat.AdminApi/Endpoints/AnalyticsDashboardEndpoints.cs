using WhereToEat.Analytics.Application;
using WhereToEat.BuildingBlocks.Auth;

namespace WhereToEat.AdminApi.Endpoints;

/// <summary>
/// The admin-scoped §7.5 operator-dashboard read endpoints (Step 23). They read the
/// <see cref="IAnalyticsRollupReader"/> aggregation seam — which groups the append-only event store
/// <b>in SQL</b> and returns counts/ratios only — so by shape no actor hash, per-event id, precise
/// coordinate, or per-user row can leave the boundary (invariant #11: venues see aggregates, never
/// personal data). Every endpoint requires the SAME admin-only policy as the other admin endpoints
/// (anonymous → 401, authenticated non-admin → 403, admin → 200); a bad time window is a descriptive
/// 400 (validated before the reader, never a 500). The operator portal (Step 18) is routed to this
/// admin host, which is why the read lives here.
/// </summary>
internal static class AnalyticsDashboardEndpoints
{
    /// <summary>The largest window the dashboard reads in one call — a guard against an open-ended scan.</summary>
    private static readonly TimeSpan MaxWindow = TimeSpan.FromDays(366);

    /// <summary>The default demand-breakdown cap when the caller omits or over-asks <c>top</c>.</summary>
    private const int DefaultDemandTop = 20;
    private const int MaxDemandTop = 100;

    public static IEndpointRouteBuilder MapAnalyticsDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        // Visibility/traffic for one venue over a window: impressions, card-opens, action clicks, CTR.
        app.MapGet("/admin/analytics/restaurants/{id:guid}/traffic", async (
                Guid id,
                DateTimeOffset? from,
                DateTimeOffset? to,
                IAnalyticsRollupReader reader,
                CancellationToken ct) =>
            {
                if (!TryValidateWindow(from, to, out var window, out var error))
                {
                    return error;
                }

                var traffic = await reader
                    .GetRestaurantTrafficAsync(id, window.From, window.To, ct)
                    .ConfigureAwait(false);
                return Results.Ok(traffic);
            })
            .WithName("AdminAnalyticsTraffic")
            .WithTags("AdminAnalytics")
            .RequireAuthorization(AdminAuthorization.PolicyName);

        // The conversion funnel for one venue over a window: impression → card-open → action.
        app.MapGet("/admin/analytics/restaurants/{id:guid}/funnel", async (
                Guid id,
                DateTimeOffset? from,
                DateTimeOffset? to,
                IAnalyticsRollupReader reader,
                CancellationToken ct) =>
            {
                if (!TryValidateWindow(from, to, out var window, out var error))
                {
                    return error;
                }

                var funnel = await reader
                    .GetConversionFunnelAsync(id, window.From, window.To, ct)
                    .ConfigureAwait(false);
                return Results.Ok(funnel);
            })
            .WithName("AdminAnalyticsFunnel")
            .WithTags("AdminAnalytics")
            .RequireAuthorization(AdminAuthorization.PolicyName);

        // Demand by category/dish over a window: the most-searched taxonomy items (a selection id is a
        // taxonomy item, never a person). Window-scoped + top-N capped; not restaurant-scoped (demand is
        // the area-wide signal, even for items a venue does not carry).
        app.MapGet("/admin/analytics/demand", async (
                DateTimeOffset? from,
                DateTimeOffset? to,
                int? top,
                IAnalyticsRollupReader reader,
                CancellationToken ct) =>
            {
                if (!TryValidateWindow(from, to, out var window, out var error))
                {
                    return error;
                }

                var demand = await reader
                    .GetDemandBreakdownAsync(window.From, window.To, ClampTop(top), ct)
                    .ConfigureAwait(false);
                return Results.Ok(demand);
            })
            .WithName("AdminAnalyticsDemand")
            .WithTags("AdminAnalytics")
            .RequireAuthorization(AdminAuthorization.PolicyName);

        // Price positioning for one venue: its per-dish prices vs the area/category median the engine
        // already materializes. No event/behavioural data at all — no window applies.
        app.MapGet("/admin/analytics/restaurants/{id:guid}/price-positioning", async (
                Guid id,
                IAnalyticsRollupReader reader,
                CancellationToken ct) =>
            {
                var positioning = await reader.GetPricePositioningAsync(id, ct).ConfigureAwait(false);
                return Results.Ok(positioning);
            })
            .WithName("AdminAnalyticsPricePositioning")
            .WithTags("AdminAnalytics")
            .RequireAuthorization(AdminAuthorization.PolicyName);

        // Ratings distribution for one venue: the cumulative all-time count/sum/average from the
        // materialized rollup (invariant #6). Aggregate-only — never a single user's score. No window.
        app.MapGet("/admin/analytics/restaurants/{id:guid}/ratings", async (
                Guid id,
                IAnalyticsRollupReader reader,
                CancellationToken ct) =>
            {
                var ratings = await reader.GetRatingsDistributionAsync(id, ct).ConfigureAwait(false);
                return Results.Ok(ratings);
            })
            .WithName("AdminAnalyticsRatings")
            .WithTags("AdminAnalytics")
            .RequireAuthorization(AdminAuthorization.PolicyName);

        return app;
    }

    /// <summary>
    /// Validates the requested <c>from</c>/<c>to</c> hour window before the reader runs: both bounds are
    /// required, <c>from</c> must not be after <c>to</c>, and the span must not exceed
    /// <see cref="MaxWindow"/>. A bad window is a descriptive 400 (the <c>{ error, message }</c> envelope
    /// the other admin endpoints use), never a 500 from an open-ended or inverted scan.
    /// </summary>
    private static bool TryValidateWindow(
        DateTimeOffset? from,
        DateTimeOffset? to,
        out (DateTimeOffset From, DateTimeOffset To) window,
        out IResult error)
    {
        window = default;

        if (from is null || to is null)
        {
            error = BadRequest(
                "Analytics.Window.Required",
                "Both 'from' and 'to' query parameters are required (ISO-8601 timestamps).");
            return false;
        }

        if (from > to)
        {
            error = BadRequest(
                "Analytics.Window.Inverted",
                "'from' must not be after 'to'.");
            return false;
        }

        if (to.Value - from.Value > MaxWindow)
        {
            error = BadRequest(
                "Analytics.Window.TooLarge",
                $"The requested window exceeds the maximum of {MaxWindow.TotalDays:0} days.");
            return false;
        }

        window = (from.Value, to.Value);
        error = Results.Empty;
        return true;
    }

    /// <summary>Clamps the demand cap to a sane positive range so the underlying TOP stays bounded.</summary>
    private static int ClampTop(int? top)
        => top is null or <= 0 ? DefaultDemandTop : Math.Min(top.Value, MaxDemandTop);

    /// <summary>The shared 400 shape — the same <c>{ error, message }</c> envelope <see cref="AdminResults"/> emits.</summary>
    private static IResult BadRequest(string code, string message)
        => Results.BadRequest(new { error = code, message });
}
