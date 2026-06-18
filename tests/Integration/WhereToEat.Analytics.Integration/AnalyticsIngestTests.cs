using Dapper;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using WhereToEat.Analytics.Application;
using WhereToEat.Analytics.Domain;
using WhereToEat.Analytics.Infrastructure.Anonymization;
using WhereToEat.Analytics.Infrastructure.Persistence;
using WhereToEat.BuildingBlocks.Persistence;
using Xunit;

namespace WhereToEat.Analytics.Integration;

/// <summary>
/// Step 11 containerized-SQL integration tests for the Analytics ingest path: the
/// <see cref="IngestEventCommandHandler"/> anonymizes a raw event (rotating-salt) and the
/// append-optimized writer persists only the anonymized shape; the aggregates-only
/// <see cref="AnalyticsRollupReader"/> returns the expected impression/CTR rollup; and a negative test
/// asserts no precise-coordinate / raw-PII column is ever populated in the stored row. Real SQL Server
/// container (Docker required).
/// </summary>
public sealed class AnalyticsIngestTests : IClassFixture<SqlServerFixture>
{
    private readonly SqlServerFixture _fixture;
    private readonly SqlConnectionFactory _connectionFactory;
    private readonly AppendOnlyAnalyticsWriter _writer;
    private readonly AnalyticsRollupReader _rollupReader;
    private readonly IngestEventCommandHandler _handler;

    public AnalyticsIngestTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
        _connectionFactory = new SqlConnectionFactory(fixture.ConnectionString);
        _writer = new AppendOnlyAnalyticsWriter(_connectionFactory);
        _rollupReader = new AnalyticsRollupReader(_connectionFactory);

        var options = new AnonymizerOptions { GeohashPrecision = 5, MasterSecret = "it-master" };
        var anonymizer = new Anonymizer(new RotatingSaltProvider(options), options);
        _handler = new IngestEventCommandHandler(anonymizer, _writer, NullLogger<IngestEventCommandHandler>.Instance);
    }

    [Fact]
    public void Migrations_CreatedTheRollupTable()
    {
        _fixture.AppliedScripts.Should().Contain(s => s.Contains("0012_create_analytics_rollups"));
    }

    [Fact]
    public async Task Ingest_Persists_OnlyTheAnonymizedShape()
    {
        var restaurant = Guid.NewGuid();
        var raw = new RawAnalyticsEvent
        {
            Kind = EventKind.CardOpen,
            OccurredAtUtc = new DateTimeOffset(2026, 6, 15, 9, 41, 22, TimeSpan.Zero),
            RestaurantId = restaurant,
            CategoryIds = [Guid.NewGuid()],
            DishIds = [Guid.NewGuid()],
            SortMode = "best",
            Filters = ["price"],
            Position = 2,
            UserId = "user-secret-001",        // dropped/hashed
            SessionId = "sess-secret",         // dropped/hashed
            Latitude = 50.4501234,             // dropped/coarsened
            Longitude = 30.5234123,            // dropped/coarsened
        };

        var result = await _handler.HandleAsync(new IngestEventCommand([raw]));
        result.IsSuccess.Should().BeTrue();

        // The stored row carries only the anonymized pieces.
        using var connection = (SqlConnection)await _connectionFactory.CreateOpenConnectionAsync();
        var row = await connection.QuerySingleAsync<(int Kind, DateTimeOffset OccurredAtHour, string? ActorHash, string? CoarseGeohash, string DimensionsJson)>(
            """
            SELECT TOP 1 Kind, OccurredAtHour, ActorHash, CoarseGeohash, DimensionsJson
            FROM analytics.AnalyticsEvent
            WHERE CAST(JSON_VALUE(DimensionsJson, '$.restaurantId') AS UNIQUEIDENTIFIER) = @Restaurant;
            """,
            new { Restaurant = restaurant });

        row.Kind.Should().Be((int)EventKind.CardOpen);
        row.OccurredAtHour.Should().Be(new DateTimeOffset(2026, 6, 15, 9, 0, 0, TimeSpan.Zero), "time is hour-truncated");

        // Hashed, not raw: a 64-char hex digest that contains neither the raw user nor session id.
        row.ActorHash.Should().NotBeNull();
        row.ActorHash!.Should().HaveLength(64).And.MatchRegex("^[0-9a-f]{64}$");
        row.ActorHash.Should().NotContain("user-secret").And.NotContain("sess-secret");

        // Coarse, not precise: a short neighbourhood-grade geohash, never the raw decimal coordinate.
        row.CoarseGeohash.Should().NotBeNullOrEmpty();
        row.CoarseGeohash!.Length.Should().BeLessThanOrEqualTo(6);
        row.CoarseGeohash.Should().NotContain("50.45").And.NotContain("30.52");

        // The retained dimensions round-trip through the JSON detail column.
        row.DimensionsJson.Should().Contain("best");
    }

    [Fact]
    public async Task RollupReader_Returns_AggregatesOnly_ImpressionsAndCtr()
    {
        var restaurant = Guid.NewGuid();
        var hour = new DateTimeOffset(2026, 7, 1, 12, 30, 0, TimeSpan.Zero);

        // 4 impressions + 1 card-open for the restaurant in the same hour -> CTR 0.25.
        var events = new List<RawAnalyticsEvent>();
        for (var i = 0; i < 4; i++)
        {
            events.Add(new RawAnalyticsEvent { Kind = EventKind.Impression, OccurredAtUtc = hour, RestaurantId = restaurant });
        }

        events.Add(new RawAnalyticsEvent { Kind = EventKind.CardOpen, OccurredAtUtc = hour, RestaurantId = restaurant });

        var result = await _handler.HandleAsync(new IngestEventCommand(events));
        result.IsSuccess.Should().BeTrue();

        var rollup = await _rollupReader.GetRestaurantHourlyRollupAsync(
            hour.AddHours(-1),
            hour.AddHours(1));

        var bucket = rollup.Should().ContainSingle(r => r.RestaurantId == restaurant).Subject;
        bucket.HourUtc.Should().Be(new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero));
        bucket.Impressions.Should().Be(4);
        bucket.CardOpens.Should().Be(1);
        bucket.Ctr.Should().BeApproximately(0.25, 1e-9);
    }

    [Fact]
    public async Task StoredRow_NeverPopulates_APreciseCoordinateOrRawPiiColumn()
    {
        // Ingest an event WITH precise coordinates + raw ids — the anonymizer must drop them.
        var restaurant = Guid.NewGuid();
        await _handler.HandleAsync(new IngestEventCommand(
        [
            new RawAnalyticsEvent
            {
                Kind = EventKind.Search,
                RestaurantId = restaurant,
                UserId = "pii-user",
                Latitude = 49.8397,
                Longitude = 24.0297,
            },
        ]));

        using var connection = (SqlConnection)await _connectionFactory.CreateOpenConnectionAsync();

        // (a) No column on the table can even hold a raw id / precise coordinate.
        var forbiddenColumns = await connection.QuerySingleAsync<int>(
            """
            SELECT COUNT(*)
            FROM sys.columns
            WHERE object_id = OBJECT_ID('analytics.AnalyticsEvent')
              AND name IN ('UserId', 'SessionId', 'Latitude', 'Longitude', 'Lat', 'Lng', 'Ip', 'IpAddress', 'Email');
            """);
        forbiddenColumns.Should().Be(0, "analytics is anonymized by schema (invariant #11)");

        // (b) The FULL raw id / FULL precise coordinate text never appears in ANY textual column of the
        //     stored row — the hash column, the coarse geohash column, the JSON detail bag, and (defensively)
        //     the row id cast to text. We match the complete precise values ('49.8397'/'24.0297'), not a
        //     truncated prefix, so the assertion self-evidently proves no precise coordinate or raw id leaked.
        var leak = await connection.QuerySingleAsync<int>(
            """
            SELECT COUNT(*)
            FROM analytics.AnalyticsEvent
            WHERE CAST(JSON_VALUE(DimensionsJson, '$.restaurantId') AS UNIQUEIDENTIFIER) = @Restaurant
              AND (ISNULL(ActorHash, '') LIKE '%pii-user%'
                   OR ISNULL(ActorHash, '') LIKE '%49.8397%'
                   OR ISNULL(ActorHash, '') LIKE '%24.0297%'
                   OR ISNULL(CoarseGeohash, '') LIKE '%pii-user%'
                   OR ISNULL(CoarseGeohash, '') LIKE '%49.8397%'
                   OR ISNULL(CoarseGeohash, '') LIKE '%24.0297%'
                   OR DimensionsJson LIKE '%pii-user%'
                   OR DimensionsJson LIKE '%49.8397%'
                   OR DimensionsJson LIKE '%24.0297%'
                   OR CAST(Id AS VARCHAR(64)) LIKE '%pii-user%'
                   OR CAST(Id AS VARCHAR(64)) LIKE '%49.8397%'
                   OR CAST(Id AS VARCHAR(64)) LIKE '%24.0297%');
            """,
            new { Restaurant = restaurant });
        leak.Should().Be(0, "the raw id and precise coordinate are dropped/hashed/coarsened at ingest");
    }
}
