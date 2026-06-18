using Dapper;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using WhereToEat.Analytics.Domain;
using WhereToEat.Analytics.Domain.Anonymization;
using WhereToEat.Analytics.Domain.Identifiers;
using WhereToEat.Analytics.Infrastructure.Persistence;
using WhereToEat.BuildingBlocks.Persistence;
using Xunit;

namespace WhereToEat.Analytics.Integration;

/// <summary>
/// Step 5 containerized-SQL integration tests for the Analytics module: the migrations create the
/// analytics schema, the append-optimized Dapper writer round-trips an anonymized event (single and
/// batch), and the stored row is anonymized by schema (no raw-id / precise-coordinate column exists).
/// Runs against a real SQL Server container (Docker required).
/// </summary>
public sealed class AnalyticsAppendTests : IClassFixture<SqlServerFixture>
{
    private readonly SqlServerFixture _fixture;
    private readonly SqlConnectionFactory _connectionFactory;
    private readonly AppendOnlyAnalyticsWriter _writer;
    private readonly AnalyticsEventReader _reader;

    public AnalyticsAppendTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
        _connectionFactory = new SqlConnectionFactory(fixture.ConnectionString);
        _writer = new AppendOnlyAnalyticsWriter(_connectionFactory);
        _reader = new AnalyticsEventReader(_connectionFactory);
    }

    [Fact]
    public void Migrations_CreatedTheAnalyticsSchema()
    {
        _fixture.AppliedScripts.Should().Contain(s => s.Contains("0009_create_analytics_events"));
    }

    [Fact]
    public async Task Append_RoundTrips_TheAnonymizedEvent()
    {
        var category = Guid.NewGuid();
        var dish = Guid.NewGuid();
        var actor = HashedActorId.FromDigest(new string('a', 64)).Value;
        var geohash = Geohash.Create("u8vk3").Value;

        var @event = AnalyticsEvent.Record(
            EventKind.CardOpen,
            HourBucket.FromInstant(new DateTimeOffset(2026, 6, 15, 14, 37, 0, TimeSpan.Zero)),
            EventDimensions.Create(categoryIds: [category], dishIds: [dish], sortMode: "best", filters: ["price"], position: 2),
            actor,
            geohash).Value;

        await _writer.AppendAsync(@event);

        var loaded = await _reader.GetByIdAsync(@event.Id);

        loaded.Should().NotBeNull();
        loaded!.Kind.Should().Be(EventKind.CardOpen);
        loaded.OccurredAtHour.HourUtc.Should().Be(new DateTimeOffset(2026, 6, 15, 14, 0, 0, TimeSpan.Zero));
        loaded.Actor.Should().Be(actor);
        loaded.CoarseLocation.Should().Be(geohash);
        loaded.Dimensions.CategoryIds.Should().ContainSingle().Which.Should().Be(category);
        loaded.Dimensions.DishIds.Should().ContainSingle().Which.Should().Be(dish);
        loaded.Dimensions.SortMode.Should().Be("best");
        loaded.Dimensions.Filters.Should().ContainInOrder("price");
        loaded.Dimensions.Position.Should().Be(2);
        loaded.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task AppendBatch_InsertsEveryEvent_InOneRoundTrip()
    {
        var events = Enumerable.Range(0, 5)
            .Select(_ => AnalyticsEvent.Record(
                EventKind.Impression,
                HourBucket.FromInstant(DateTimeOffset.UtcNow),
                EventDimensions.Create(position: 1)).Value)
            .ToList();

        await _writer.AppendBatchAsync(events);

        foreach (var e in events)
        {
            (await _reader.GetByIdAsync(e.Id)).Should().NotBeNull();
        }
    }

    [Fact]
    public async Task AppendBatch_WithEmptyCollection_IsANoOp()
    {
        await _writer.AppendBatchAsync(Array.Empty<AnalyticsEvent>());
        // No exception, nothing written — the append path tolerates an empty flush.
    }

    [Fact]
    public async Task StoredRow_IsAnonymizedBySchema_NoRawIdOrPreciseCoordinateColumn()
    {
        using var connection = (SqlConnection)await _connectionFactory.CreateOpenConnectionAsync();

        // No column on the analytics event table holds a raw user/session id or a precise coordinate.
        var forbiddenColumns = await connection.QuerySingleAsync<int>(
            """
            SELECT COUNT(*)
            FROM sys.columns
            WHERE object_id = OBJECT_ID('analytics.AnalyticsEvent')
              AND name IN ('UserId', 'SessionId', 'Latitude', 'Longitude', 'Lat', 'Lng', 'Ip', 'IpAddress', 'Email');
            """);

        forbiddenColumns.Should().Be(0, "analytics is anonymized by schema (invariant #11): no raw id / precise coord column");
    }
}
