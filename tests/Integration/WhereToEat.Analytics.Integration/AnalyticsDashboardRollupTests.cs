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
/// Step 23 containerized-SQL integration tests for the §7.5 dashboard aggregate queries the
/// <see cref="AnalyticsRollupReader"/> exposes: visibility/traffic, the conversion funnel, demand by
/// category/dish, price positioning, and the ratings distribution. Each test seeds anonymized events
/// (and, where needed, the catalog menu + the precomputed price-median + the ratings rollup) and asserts
/// the reader returns correctly-grouped <b>counts/ratios only</b> — and a dedicated test asserts the
/// reader's result records carry NO per-user / per-event / precise-coordinate field by shape (invariant
/// #11). Real SQL Server container (Docker required).
/// </summary>
public sealed class AnalyticsDashboardRollupTests : IClassFixture<SqlServerFixture>
{
    /// <summary>Identity/precise-coordinate property names that must NEVER appear on an aggregate record.</summary>
    private static readonly string[] IdentifyingFieldNames =
        ["ActorHash", "UserId", "SessionId", "Latitude", "Longitude", "Geohash", "CoarseGeohash", "EventId"];

    private readonly SqlConnectionFactory _connectionFactory;
    private readonly AnalyticsRollupReader _reader;
    private readonly IngestEventCommandHandler _handler;

    public AnalyticsDashboardRollupTests(SqlServerFixture fixture)
    {
        _connectionFactory = new SqlConnectionFactory(fixture.ConnectionString);
        var writer = new AppendOnlyAnalyticsWriter(_connectionFactory);
        _reader = new AnalyticsRollupReader(_connectionFactory);

        var options = new AnonymizerOptions { GeohashPrecision = 5, MasterSecret = "it-master" };
        var anonymizer = new Anonymizer(new RotatingSaltProvider(options), options);
        _handler = new IngestEventCommandHandler(anonymizer, writer, NullLogger<IngestEventCommandHandler>.Instance);
    }

    [Fact]
    public async Task GetRestaurantTraffic_CountsImpressions_CardOpens_Actions_AndCtr()
    {
        var restaurant = Guid.NewGuid();
        var hour = new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero);

        // 10 impressions, 4 card-opens, 2 actions for the venue in the window -> CTR 0.4.
        await IngestAsync(
            Many(EventKind.Impression, restaurant, hour, 10)
                .Concat(Many(EventKind.CardOpen, restaurant, hour, 4))
                .Concat(Many(EventKind.Action, restaurant, hour, 2))
                // A different restaurant's events must NOT bleed into this venue's counts.
                .Concat(Many(EventKind.Impression, Guid.NewGuid(), hour, 7))
                .ToList());

        var traffic = await _reader.GetRestaurantTrafficAsync(restaurant, hour.AddHours(-1), hour.AddHours(1));

        traffic.RestaurantId.Should().Be(restaurant);
        traffic.Impressions.Should().Be(10);
        traffic.CardOpens.Should().Be(4);
        traffic.Actions.Should().Be(2);
        traffic.Ctr.Should().BeApproximately(0.4, 1e-9);
    }

    [Fact]
    public async Task GetConversionFunnel_ReturnsStageCounts_AndStageRatios()
    {
        var restaurant = Guid.NewGuid();
        var hour = new DateTimeOffset(2026, 8, 2, 14, 0, 0, TimeSpan.Zero);

        // 20 impressions -> 5 card-opens (0.25) -> 1 action (0.2).
        await IngestAsync(
            Many(EventKind.Impression, restaurant, hour, 20)
                .Concat(Many(EventKind.CardOpen, restaurant, hour, 5))
                .Concat(Many(EventKind.Action, restaurant, hour, 1))
                .ToList());

        var funnel = await _reader.GetConversionFunnelAsync(restaurant, hour.AddHours(-1), hour.AddHours(1));

        funnel.Impressions.Should().Be(20);
        funnel.CardOpens.Should().Be(5);
        funnel.Actions.Should().Be(1);
        funnel.ImpressionToCardOpenRate.Should().BeApproximately(0.25, 1e-9);
        funnel.CardOpenToActionRate.Should().BeApproximately(0.2, 1e-9);
    }

    [Fact]
    public async Task GetConversionFunnel_EmptyWindow_IsZeroedNotNaN()
    {
        var restaurant = Guid.NewGuid();
        var hour = new DateTimeOffset(2026, 8, 3, 9, 0, 0, TimeSpan.Zero);

        var funnel = await _reader.GetConversionFunnelAsync(restaurant, hour, hour.AddHours(1));

        funnel.Impressions.Should().Be(0);
        funnel.CardOpens.Should().Be(0);
        funnel.Actions.Should().Be(0);
        funnel.ImpressionToCardOpenRate.Should().Be(0d);
        funnel.CardOpenToActionRate.Should().Be(0d);
    }

    [Fact]
    public async Task GetDemandBreakdown_GroupsSearchCounts_ByCategoryAndDish_Capped()
    {
        var hour = new DateTimeOffset(2026, 8, 4, 11, 0, 0, TimeSpan.Zero);
        var hotCategory = Guid.NewGuid();
        var coldCategory = Guid.NewGuid();
        var hotDish = Guid.NewGuid();

        // Fresh ids per test scope the assertion (the event store accumulates across tests). 4 searches
        // reference hotCategory+hotDish, 1 references coldCategory only -> hotCategory=4, hotDish=4,
        // coldCategory=1.
        var searches = new List<RawAnalyticsEvent>();
        for (var i = 0; i < 4; i++)
        {
            searches.Add(Search(hour, categories: [hotCategory], dishes: [hotDish]));
        }

        searches.Add(Search(hour, categories: [coldCategory], dishes: []));

        await IngestAsync(searches);

        var demand = await _reader.GetDemandBreakdownAsync(hour.AddHours(-1), hour.AddHours(1), top: 50);

        var hot = demand.Categories.Should().ContainSingle(c => c.SelectionId == hotCategory).Subject;
        hot.SearchCount.Should().Be(4);
        demand.Categories.Should().Contain(c => c.SelectionId == coldCategory && c.SearchCount == 1);

        demand.Dishes.Should().Contain(d => d.SelectionId == hotDish && d.SearchCount == 4);
    }

    [Fact]
    public async Task GetDemandBreakdown_NonPositiveTop_ReturnsEmpty_NoQuery()
    {
        var hour = new DateTimeOffset(2026, 8, 5, 8, 0, 0, TimeSpan.Zero);

        var demand = await _reader.GetDemandBreakdownAsync(hour, hour.AddHours(1), top: 0);

        demand.Categories.Should().BeEmpty();
        demand.Dishes.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPricePositioning_ReturnsVenuePriceVsMaterializedMedian_NoEventData()
    {
        var restaurant = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var dishWithMedian = Guid.NewGuid();
        var dishNoMedian = Guid.NewGuid();

        await using (var conn = new SqlConnection(await GetConnStringAsync()))
        {
            await conn.OpenAsync();
            await SeedCategoryAsync(conn, categoryId);
            await SeedDishAsync(conn, dishWithMedian, categoryId, "Борщ");
            await SeedDishAsync(conn, dishNoMedian, categoryId, "Узвар");
            await SeedRestaurantAsync(conn, restaurant);
            await SeedMenuItemAsync(conn, restaurant, dishWithMedian, 120m, "UAH");
            await SeedMenuItemAsync(conn, restaurant, dishNoMedian, 30m, "UAH");
            // City-wide median (AreaKey '*') for the first dish only.
            await SeedDishMedianAsync(conn, dishWithMedian, "UAH", 150m);
        }

        var positioning = await _reader.GetPricePositioningAsync(restaurant);

        positioning.RestaurantId.Should().Be(restaurant);

        var withMedian = positioning.Dishes.Should().ContainSingle(d => d.DishId == dishWithMedian).Subject;
        withMedian.VenuePrice.Should().Be(120m);
        withMedian.MedianPrice.Should().Be(150m);
        withMedian.DeltaFromMedian.Should().Be(-30m, "the venue is 30 cheaper than the market median");
        withMedian.Currency.Should().Be("UAH");

        var noMedian = positioning.Dishes.Should().ContainSingle(d => d.DishId == dishNoMedian).Subject;
        noMedian.VenuePrice.Should().Be(30m);
        noMedian.MedianPrice.Should().BeNull();
        noMedian.DeltaFromMedian.Should().BeNull();
    }

    [Fact]
    public async Task GetRatingsDistribution_ReadsCumulativeAggregate_AndAverages()
    {
        var rated = Guid.NewGuid();
        var unrated = Guid.NewGuid();

        await using (var conn = new SqlConnection(await GetConnStringAsync()))
        {
            await conn.OpenAsync();
            // 10 ratings summing to 42 -> average 4.2.
            await conn.ExecuteAsync(
                "INSERT INTO ratings.RatingAggregate (RestaurantId, ScoreSum, ScoreCount) VALUES (@Id, @Sum, @Count);",
                new { Id = rated, Sum = 42d, Count = 10L });
        }

        var distribution = await _reader.GetRatingsDistributionAsync(rated);
        distribution.RestaurantId.Should().Be(rated);
        distribution.RatingCount.Should().Be(10);
        distribution.ScoreSum.Should().Be(42d);
        distribution.AverageScore.Should().BeApproximately(4.2, 1e-9);

        // No aggregate row -> a zeroed distribution, not a divide-by-zero.
        var none = await _reader.GetRatingsDistributionAsync(unrated);
        none.RatingCount.Should().Be(0);
        none.ScoreSum.Should().Be(0d);
        none.AverageScore.Should().Be(0d);
    }

    /// <summary>
    /// Every aggregate result record the reader returns (including the nested per-row records inside the
    /// list-bearing aggregates) statically asserted to carry NO per-user / per-event / precise-coordinate
    /// field by shape — invariant #11. New §7.5 record types are covered here as well as the Step 11 one.
    /// </summary>
    public static IEnumerable<object[]> AggregateRecordTypes() =>
    [
        [typeof(RestaurantHourlyRollup)],
        [typeof(RestaurantTrafficSummary)],
        [typeof(DemandBreakdown)],
        [typeof(DemandCount)],
        [typeof(PricePositioning)],
        [typeof(DishPricePosition)],
        [typeof(RatingsDistribution)],
        [typeof(ConversionFunnel)],
    ];

    [Theory]
    [MemberData(nameof(AggregateRecordTypes))]
    public void AggregateRecord_CarriesNoIdentifyingField_ByShape(Type recordType)
    {
        // The aggregate records expose only counts/ratios/prices + a taxonomy or restaurant id (the venue
        // asking for its own dashboard). No actor-hash / per-event-id / coordinate property exists on any
        // of them — by shape, invariant #11 cannot be breached through this boundary.
        var props = recordType.GetProperties().Select(p => p.Name).ToList();

        props.Should().NotContain(IdentifyingFieldNames);
    }

    [Fact]
    public void TrafficSummary_ExposesOnlyCountsRatiosAndRestaurantId_ByShape()
    {
        var props = typeof(RestaurantTrafficSummary).GetProperties().Select(p => p.Name).ToList();

        props.Should().BeEquivalentTo("RestaurantId", "Impressions", "CardOpens", "Actions", "Ctr");
    }

    private async Task<string> GetConnStringAsync()
    {
        // The factory does not expose the raw string; round-trip a throwaway open connection to read it.
        using var probe = (SqlConnection)await _connectionFactory.CreateOpenConnectionAsync();
        return probe.ConnectionString;
    }

    private async Task IngestAsync(IReadOnlyList<RawAnalyticsEvent> events)
    {
        var result = await _handler.HandleAsync(new IngestEventCommand(events));
        result.IsSuccess.Should().BeTrue();
    }

    private static IEnumerable<RawAnalyticsEvent> Many(EventKind kind, Guid restaurant, DateTimeOffset hour, int count)
        => Enumerable.Range(0, count).Select(_ => new RawAnalyticsEvent
        {
            Kind = kind,
            OccurredAtUtc = hour,
            RestaurantId = restaurant,
        });

    private static RawAnalyticsEvent Search(DateTimeOffset hour, IReadOnlyList<Guid> categories, IReadOnlyList<Guid> dishes)
        => new()
        {
            Kind = EventKind.Search,
            OccurredAtUtc = hour,
            CategoryIds = categories,
            DishIds = dishes,
        };

    private static Task<int> SeedCategoryAsync(SqlConnection conn, Guid id)
        => conn.ExecuteAsync("INSERT INTO catalog.Category (Id, Name) VALUES (@Id, @Name);", new { Id = id, Name = "Перші страви" });

    private static Task<int> SeedDishAsync(SqlConnection conn, Guid id, Guid categoryId, string name)
        => conn.ExecuteAsync(
            "INSERT INTO catalog.Dish (Id, CategoryId, CanonicalName) VALUES (@Id, @CategoryId, @Name);",
            new { Id = id, CategoryId = categoryId, Name = name });

    private static Task<int> SeedRestaurantAsync(SqlConnection conn, Guid id)
        => conn.ExecuteAsync(
            "INSERT INTO catalog.Restaurant (Id, Name, AddressLine, AddressCity) VALUES (@Id, @Name, @Line, @City);",
            new { Id = id, Name = "Тест Кафе", Line = "вул. Тестова, 1", City = "Львів" });

    private static Task<int> SeedMenuItemAsync(SqlConnection conn, Guid restaurantId, Guid dishId, decimal price, string currency)
        => conn.ExecuteAsync(
            "INSERT INTO catalog.MenuItem (Id, RestaurantId, DishId, PriceAmount, PriceCurrency, Source, DoNotParse) " +
            "VALUES (@Id, @RestaurantId, @DishId, @Price, @Currency, 0, 0);",
            new { Id = Guid.NewGuid(), RestaurantId = restaurantId, DishId = dishId, Price = price, Currency = currency });

    private static Task<int> SeedDishMedianAsync(SqlConnection conn, Guid dishId, string currency, decimal median)
        => conn.ExecuteAsync(
            "INSERT INTO recommendation.PriceMedian (AreaKey, DishId, MedianAmount, Currency, SampleSize) " +
            "VALUES ('*', @DishId, @Median, @Currency, 25);",
            new { DishId = dishId, Median = median, Currency = currency });
}
