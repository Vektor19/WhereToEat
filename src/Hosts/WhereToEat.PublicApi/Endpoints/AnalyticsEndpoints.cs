using WhereToEat.Analytics.Application;
using WhereToEat.Analytics.Domain;

namespace WhereToEat.PublicApi.Endpoints;

/// <summary>
/// The <c>POST /analytics/events</c> ingest endpoint (§8.1). It accepts a batch of <b>raw</b> event
/// payloads (impression / view / card_open / action / search / filter / rating_given / geo / session)
/// and hands them to the <see cref="IngestEventCommandHandler"/>, which <b>anonymizes every event
/// before persistence</b> (retain/hash/drop — invariant #11). The wire body never reaches the database:
/// the raw id / precise lat/lng are dropped/hashed/coarsened in the handler. Anonymous — clients report
/// their own behaviour; an unknown event kind or an out-of-range coordinate maps to a descriptive 400
/// (never a 500), the happy path returns 202 Accepted.
/// </summary>
internal static class AnalyticsEndpoints
{
    public static IEndpointRouteBuilder MapAnalyticsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/analytics/events", async (IngestEventsRequestBody body, IngestEventCommandHandler handler, CancellationToken ct) =>
            {
                var command = body.TryToCommand();
                if (command.IsFailureBody)
                {
                    return Results.BadRequest(new { error = command.ErrorCode, message = command.ErrorMessage });
                }

                var result = await handler.HandleAsync(command.Value!, ct).ConfigureAwait(false);

                return result.IsSuccess
                    ? Results.Accepted()
                    : Results.BadRequest(new { error = result.Error.Code, message = result.Error.Message });
            })
            .WithName("IngestAnalyticsEvents")
            .WithTags("Analytics")
            .AllowAnonymous();

        return app;
    }
}

/// <summary>
/// The wire body for <c>POST /analytics/events</c>: a list of raw events. It validates the event kind
/// into the <see cref="EventKind"/> enum before building the command, so an unknown kind is a clean 400
/// rather than an unhandled bind failure. The raw id / precise lat/lng fields ride here only as far as
/// the anonymizer.
/// </summary>
internal sealed record IngestEventsRequestBody(IReadOnlyList<RawEventBody>? Events)
{
    /// <summary>
    /// Validates and converts the body to an <see cref="IngestEventCommand"/>. Returns a body-level
    /// failure (mapped to 400) for an empty list or an unrecognised event kind; the remaining
    /// anonymization validation (coordinate range) happens in the handler.
    /// </summary>
    public BodyConversion TryToCommand()
    {
        if (Events is null || Events.Count == 0)
        {
            return BodyConversion.Failure("Analytics.NoEvents", "At least one analytics event is required.");
        }

        var raw = new List<RawAnalyticsEvent>(Events.Count);
        foreach (var e in Events)
        {
            if (!Enum.TryParse<EventKind>(e.Kind, ignoreCase: true, out var kind) || !Enum.IsDefined(kind))
            {
                return BodyConversion.Failure("Analytics.UnknownKind", $"Unknown analytics event kind '{e.Kind}'.");
            }

            raw.Add(new RawAnalyticsEvent
            {
                Kind = kind,
                OccurredAtUtc = e.OccurredAtUtc ?? DateTimeOffset.UtcNow,
                RestaurantId = e.RestaurantId,
                CategoryIds = e.CategoryIds,
                DishIds = e.DishIds,
                SortMode = e.SortMode,
                Filters = e.Filters,
                Position = e.Position,
                UserId = e.UserId,
                SessionId = e.SessionId,
                Latitude = e.Latitude,
                Longitude = e.Longitude,
            });
        }

        return BodyConversion.Success(new IngestEventCommand(raw));
    }
}

/// <summary>
/// One raw event on the wire. <see cref="UserId"/>/<see cref="SessionId"/> and
/// <see cref="Latitude"/>/<see cref="Longitude"/> are the only identifying fields — they are
/// dropped/hashed/coarsened by the anonymizer and never persisted.
/// </summary>
internal sealed record RawEventBody(
    string Kind,
    DateTimeOffset? OccurredAtUtc,
    Guid? RestaurantId,
    IReadOnlyList<Guid>? CategoryIds,
    IReadOnlyList<Guid>? DishIds,
    string? SortMode,
    IReadOnlyList<string>? Filters,
    int? Position,
    string? UserId,
    string? SessionId,
    double? Latitude,
    double? Longitude);

/// <summary>
/// A tiny body-validation outcome: either an <see cref="IngestEventCommand"/> or a body-level error
/// code/message. Keeps the endpoint's wire validation separate from the handler's domain validation
/// without leaking a SharedKernel <c>Result</c> into the host's wire types.
/// </summary>
internal sealed record BodyConversion(IngestEventCommand? Value, string? ErrorCode, string? ErrorMessage)
{
    public bool IsFailureBody => Value is null;

    public static BodyConversion Success(IngestEventCommand command) => new(command, null, null);

    public static BodyConversion Failure(string code, string message) => new(null, code, message);
}
