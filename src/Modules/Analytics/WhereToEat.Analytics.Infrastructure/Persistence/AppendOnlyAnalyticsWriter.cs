using System.Text.Json;
using Dapper;
using WhereToEat.Analytics.Domain;
using WhereToEat.Analytics.Domain.Abstractions;
using WhereToEat.BuildingBlocks.Persistence;

namespace WhereToEat.Analytics.Infrastructure.Persistence;

/// <summary>
/// The Dapper-backed, append-optimized <see cref="IAnalyticsEventStore"/>. Events are immutable facts,
/// so this only ever inserts. A batch is sent as one parameterised command (Dapper expands the
/// enumerable of parameter objects into a batched execution), keeping the write path cheap on the
/// high-volume ingest side. The retained dimensions are serialized to a JSON detail column; the actor
/// hash and coarse geohash are stored as their already-anonymized strings (no raw id / precise
/// coordinate exists to store). No EF Core — Dapper only (EF is Admin.Infrastructure-only).
/// </summary>
public sealed class AppendOnlyAnalyticsWriter : IAnalyticsEventStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly ISqlConnectionFactory _connectionFactory;

    public AppendOnlyAnalyticsWriter(ISqlConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    /// <inheritdoc />
    public Task AppendAsync(AnalyticsEvent analyticsEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(analyticsEvent);
        return AppendBatchAsync([analyticsEvent], cancellationToken);
    }

    /// <inheritdoc />
    public async Task AppendBatchAsync(IReadOnlyCollection<AnalyticsEvent> events, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0)
        {
            return;
        }

        var parameters = events.Select(ToParameters).ToArray();

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        // Passing the array of parameter objects makes Dapper batch the INSERT in one round-trip.
        await connection.ExecuteAsync(
            new CommandDefinition(AnalyticsSql.InsertEvent, parameters, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    internal static object ToParameters(AnalyticsEvent @event) => new
    {
        Id = @event.Id.Value,
        Kind = (int)@event.Kind,
        OccurredAtHour = @event.OccurredAtHour.HourUtc,
        ActorHash = @event.Actor?.Digest,
        CoarseGeohash = @event.CoarseLocation?.Value,
        DimensionsJson = JsonSerializer.Serialize(EventDimensionsDocument.From(@event.Dimensions), SerializerOptions),
    };
}
