using System.Text.Json;
using Dapper;
using WhereToEat.Analytics.Domain;
using WhereToEat.Analytics.Domain.Anonymization;
using WhereToEat.Analytics.Domain.Identifiers;
using WhereToEat.BuildingBlocks.Persistence;

namespace WhereToEat.Analytics.Infrastructure.Persistence;

/// <summary>
/// A read-back helper for the append-only analytics store. The module proper only appends; this reader
/// exists so the append round-trip is verifiable (the integration test) and as the seed for the Step 11
/// minimal rollup. Internal — exposed to the integration test via <c>InternalsVisibleTo</c>, not part
/// of the public module surface.
/// </summary>
internal sealed class AnalyticsEventReader
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly ISqlConnectionFactory _connectionFactory;

    public AnalyticsEventReader(ISqlConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    /// <summary>Rehydrates a stored anonymized event, or returns null if no such row exists.</summary>
    public async Task<AnalyticsEvent?> GetByIdAsync(AnalyticsEventId id, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        var row = await connection.QuerySingleOrDefaultAsync<AnalyticsEventRow>(
            new CommandDefinition(AnalyticsSql.SelectEventById, new { Id = id.Value }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        if (row is null)
        {
            return null;
        }

        var document = JsonSerializer.Deserialize<EventDimensionsDocument>(row.DimensionsJson, SerializerOptions)
            ?? throw new InvalidOperationException($"Stored analytics dimensions for event '{row.Id}' could not be deserialized.");

        HashedActorId? actor = null;
        if (!string.IsNullOrEmpty(row.ActorHash))
        {
            var actorResult = HashedActorId.FromDigest(row.ActorHash);
            if (actorResult.IsFailure)
            {
                // A stored row that violates the domain invariants is a data-integrity bug, not
                // recoverable input — surface it loudly rather than returning a silently-wrong event.
                throw new InvalidOperationException(
                    $"Stored actor hash for analytics event '{row.Id}' is invalid: {actorResult.Error.Message}");
            }

            actor = actorResult.Value;
        }

        Geohash? geohash = null;
        if (!string.IsNullOrEmpty(row.CoarseGeohash))
        {
            var geohashResult = Geohash.Create(row.CoarseGeohash);
            if (geohashResult.IsFailure)
            {
                throw new InvalidOperationException(
                    $"Stored coarse geohash for analytics event '{row.Id}' is invalid: {geohashResult.Error.Message}");
            }

            geohash = geohashResult.Value;
        }

        var rebuilt = AnalyticsEvent.Record(
            AnalyticsEventId.From(row.Id),
            (EventKind)row.Kind,
            HourBucket.FromInstant(row.OccurredAtHour),
            document.ToDomain(),
            actor,
            geohash);

        if (rebuilt.IsFailure)
        {
            throw new InvalidOperationException(
                $"Stored analytics event '{row.Id}' is invalid: {rebuilt.Error.Message}");
        }

        var @event = rebuilt.Value;
        @event.ClearDomainEvents();
        return @event;
    }
}
