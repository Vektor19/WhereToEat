using Dapper;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using WhereToEat.BuildingBlocks.Persistence;
using WhereToEat.Contracts.Geo;
using WhereToEat.Contracts.Parsing;
using WhereToEat.Parsing.Application;
using WhereToEat.Parsing.Application.Compliance;
using WhereToEat.Parsing.Domain;
using WhereToEat.Parsing.Infrastructure;
using WhereToEat.Parsing.Infrastructure.Examples;
using WhereToEat.Parsing.Infrastructure.Persistence;
using WhereToEat.Parsing.Infrastructure.Strategies;
using WhereToEat.Parsing.Integration.Fixtures;
using Xunit;

namespace WhereToEat.Parsing.Integration;

/// <summary>
/// Step 9 quarantine integration test (the main invariant-#2 gate): the full
/// <see cref="RunParseCommandHandler"/> runs over the pinned Borsch Cafe fixture (which contains the
/// unmappable dish "Узвар" plus mappable "Борщ"/"Вареники"), using the <b>real</b>
/// <see cref="DapperParseQuarantineStore"/> against a migrated SQL Server (Testcontainers). It proves:
/// the unmappable item lands in <c>parsing.ParseQuarantine</c>, the parse does NOT fail, and the
/// mappable items still reach the persist seam as live menu items. The cross-module catalog persist is
/// faked (Catalog owns that store); only the quarantine table is real here.
/// </summary>
public sealed class QuarantinePersistenceTests : IClassFixture<SqlServerFixture>
{
    private readonly SqlServerFixture _fixture;

    public QuarantinePersistenceTests(SqlServerFixture fixture) => _fixture = fixture;

    [Fact]
    public void Migrations_CreatedTheQuarantineTable()
    {
        _fixture.AppliedScripts.Should().Contain(s => s.Contains("0011_create_parse_quarantine"));
    }

    [Fact]
    public async Task UnmappableDish_LandsInQuarantine_ParseSucceeds_MappableStillPersist()
    {
        // ---- Dish resolver: Борщ/Вареники map; Узвар does not (-> quarantine) -------------------
        var resolver = Substitute.For<ICatalogDishResolver>();
        var borschId = Guid.NewGuid();
        var varenykyId = Guid.NewGuid();
        resolver.ResolveAsync("Борщ", Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new CanonicalDishDto(borschId, Guid.NewGuid()));
        resolver.ResolveAsync("Вареники", Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new CanonicalDishDto(varenykyId, Guid.NewGuid()));
        resolver.ResolveAsync("Узвар", Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns((CanonicalDishDto?)null);

        // ---- Catalog persist seam (faked — Catalog owns the real store) -------------------------
        var menuWriter = Substitute.For<ICatalogMenuWriter>();
        menuWriter.GetProtectionContextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new RestaurantProtectionContextDto(null, DoNotUpdate: false, new HashSet<Guid>()));
        IReadOnlyList<ResolvedMenuItemDto>? persisted = null;
        menuWriter.PersistAsync(
                Arg.Any<ParsedRestaurantFactsDto>(),
                Arg.Do<IReadOnlyList<ResolvedMenuItemDto>>(items => persisted = items),
                Arg.Any<CancellationToken>())
            .Returns(Guid.NewGuid());

        // ---- Compliance gates: all pass (the fixture is file://; we exercise the pipeline) -------
        var allowList = Substitute.For<IFirstPartyAllowList>();
        allowList.IsFirstParty(Arg.Any<string>()).Returns(true);
        var robots = Substitute.For<IRobotsTxtGate>();
        robots.IsAllowedAsync(Arg.Any<Uri>(), Arg.Any<CancellationToken>()).Returns(true);
        var rateLimiter = Substitute.For<IHostRateLimiter>();
        rateLimiter.TryAcquireAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        var geocoder = Substitute.For<IRestaurantGeocoder>();
        geocoder.GeocodeAndStoreAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);

        // ---- Real wiring: AngleSharp strategy over the fixture + real quarantine store -----------
        var angleSharp = new AngleSharpParserStrategy(NullLogger<AngleSharpParserStrategy>.Instance);
        // The fixture is static HTML, so we parse it with AngleSharp under the venue's key (the
        // strategy KIND is interchangeable for parsing this shape — the e2e drives the real Selenium).
        var pizzaParser = new PizzaHouseAngleSharpParser(angleSharp);
        var selector = new ParserStrategySelector(new IRestaurantParser[] { pizzaParser });

        var normalizer = new Normalizer(resolver, new FakeTimeProvider(), NullLogger<Normalizer>.Instance);
        var quarantineStore = new DapperParseQuarantineStore(new SqlConnectionFactory(_fixture.ConnectionString));

        var handler = new RunParseCommandHandler(
            allowList, robots, rateLimiter, selector, normalizer, quarantineStore, menuWriter, geocoder,
            NullLogger<RunParseCommandHandler>.Instance);

        var source = new SourceDescriptor(
            "Борщ Кафе",
            FixtureFile.Url("borsch-cafe/menu.html"),
            PizzaHouseAngleSharpParser.Key);

        // ---- Act ---------------------------------------------------------------------------------
        var result = await handler.HandleAsync(new RunParseCommand(source));

        // ---- Assert: parse succeeded, mappable persisted, unmappable quarantined -----------------
        result.Outcome.Should().Be(RunParseOutcome.Persisted, "the parse must not fail on an unmappable dish");
        result.PersistedItemCount.Should().Be(2, "Борщ and Вареники are mappable");
        result.QuarantinedItemCount.Should().Be(1, "Узвар is unmappable");

        persisted.Should().NotBeNull();
        persisted!.Select(p => p.DishId).Should().BeEquivalentTo(new[] { borschId, varenykyId });

        // The unmappable item is in the real quarantine table — never written as a live menu item.
        using var connection = await new SqlConnectionFactory(_fixture.ConnectionString).CreateOpenConnectionAsync();
        var rows = (await connection.QueryAsync<(string RawName, decimal PriceAmount, string RestaurantName)>(
            "SELECT RawName, PriceAmount, RestaurantName FROM parsing.ParseQuarantine WHERE RestaurantName = @Name",
            new { Name = "Борщ Кафе" })).ToList();

        rows.Should().ContainSingle();
        rows[0].RawName.Should().Be("Узвар");
        rows[0].PriceAmount.Should().Be(45m);
    }
}
