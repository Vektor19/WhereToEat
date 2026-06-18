using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WhereToEat.Contracts.Geo;
using WhereToEat.Contracts.Parsing;
using WhereToEat.Parsing.Application;
using WhereToEat.Parsing.Application.Compliance;
using WhereToEat.Parsing.Domain;
using WhereToEat.SharedKernel.Results;
using WhereToEat.SharedKernel.ValueObjects;
using Xunit;

namespace WhereToEat.Parsing.UnitTests;

/// <summary>
/// The parse pipeline orchestration (invariants #9 + #3 + #2): compliance gates fire BEFORE any fetch
/// (a refused gate ends the run without the strategy ever being invoked), the admin-protection persist
/// gate is enforced before writing (a <c>DoNotUpdate</c> venue is skipped entirely; a <c>DoNotParse</c>
/// dish is excluded from the write), and unmappable dishes are quarantined (not dropped, not fatal)
/// even when the venue is then skipped.
/// </summary>
public sealed class RunParseCommandHandlerTests
{
    private static Money Uah(decimal amount) => Money.Create(amount, "UAH").Value;

    private sealed class Harness
    {
        public IFirstPartyAllowList AllowList { get; } = Substitute.For<IFirstPartyAllowList>();
        public IRobotsTxtGate Robots { get; } = Substitute.For<IRobotsTxtGate>();
        public IHostRateLimiter RateLimiter { get; } = Substitute.For<IHostRateLimiter>();
        public IParserStrategySelector Selector { get; } = Substitute.For<IParserStrategySelector>();
        public IRestaurantParser Parser { get; } = Substitute.For<IRestaurantParser>();
        public INormalizer Normalizer { get; } = Substitute.For<INormalizer>();
        public IParseQuarantineStore Quarantine { get; } = Substitute.For<IParseQuarantineStore>();
        public ICatalogMenuWriter MenuWriter { get; } = Substitute.For<ICatalogMenuWriter>();
        public IRestaurantGeocoder Geocoder { get; } = Substitute.For<IRestaurantGeocoder>();

        public Harness()
        {
            // Default: all gates pass, strategy resolves, geocode succeeds.
            AllowList.IsFirstParty(Arg.Any<string>()).Returns(true);
            Robots.IsAllowedAsync(Arg.Any<Uri>(), Arg.Any<CancellationToken>()).Returns(true);
            RateLimiter.TryAcquireAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
            Selector.Resolve(Arg.Any<string>()).Returns(Result.Success(Parser));
            Geocoder.GeocodeAndStoreAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
            MenuWriter.PersistAsync(Arg.Any<ParsedRestaurantFactsDto>(), Arg.Any<IReadOnlyList<ResolvedMenuItemDto>>(), Arg.Any<CancellationToken>())
                .Returns(Guid.NewGuid());
        }

        public RunParseCommandHandler Build() => new(
            AllowList, Robots, RateLimiter, Selector, Normalizer, Quarantine, MenuWriter, Geocoder,
            NullLogger<RunParseCommandHandler>.Instance);
    }

    private static ParsedMenu SampleMenu() => new(
        new ParsedRestaurantFacts("Борщ Кафе", "вул. Хрещатик, 1", "Київ", Array.Empty<ParsedContactLink>()),
        Array.Empty<ParsedDish>());

    private static RunParseCommand Command() =>
        new(new SourceDescriptor("Борщ Кафе", new Uri("https://borsch-cafe.example/menu"), "selenium"));

    private static void GivenParsedMenu(Harness h, ParsedMenu menu)
        => h.Parser.ParseAsync(Arg.Any<SourceDescriptor>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(menu));

    private static void GivenNormalization(Harness h, NormalizationResult result)
        => h.Normalizer.NormalizeAsync(Arg.Any<ParsedMenu>(), Arg.Any<CancellationToken>()).Returns(result);

    private static void GivenProtection(Harness h, RestaurantProtectionContextDto context)
        => h.MenuWriter.GetProtectionContextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(context);

    // ---- Compliance gates fire BEFORE fetch (invariant #9) --------------------------------------

    [Fact]
    public async Task NonFirstPartyHost_IsRejected_BeforeAnyFetch()
    {
        var h = new Harness();
        h.AllowList.IsFirstParty(Arg.Any<string>()).Returns(false);

        var result = await h.Build().HandleAsync(Command());

        result.Outcome.Should().Be(RunParseOutcome.RejectedNotFirstParty);
        await h.Parser.DidNotReceive().ParseAsync(Arg.Any<SourceDescriptor>(), Arg.Any<CancellationToken>());
        await h.Robots.DidNotReceive().IsAllowedAsync(Arg.Any<Uri>(), Arg.Any<CancellationToken>());
        // Pin the gate ORDER: the next gate down (rate limit) must not even be consulted.
        await h.RateLimiter.DidNotReceive().TryAcquireAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RobotsDisallow_BlocksTheParse_BeforeAnyFetch()
    {
        var h = new Harness();
        h.Robots.IsAllowedAsync(Arg.Any<Uri>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await h.Build().HandleAsync(Command());

        result.Outcome.Should().Be(RunParseOutcome.BlockedByRobots);
        await h.Parser.DidNotReceive().ParseAsync(Arg.Any<SourceDescriptor>(), Arg.Any<CancellationToken>());
        // Pin the gate ORDER: the next gate down (rate limit) must not even be consulted.
        await h.RateLimiter.DidNotReceive().TryAcquireAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RateLimit_BlocksTheParse_BeforeAnyFetch()
    {
        var h = new Harness();
        h.RateLimiter.TryAcquireAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await h.Build().HandleAsync(Command());

        result.Outcome.Should().Be(RunParseOutcome.BlockedByRateLimit);
        await h.Parser.DidNotReceive().ParseAsync(Arg.Any<SourceDescriptor>(), Arg.Any<CancellationToken>());
        // Pin the gate ORDER: the next gate down (strategy resolution) must not even be consulted.
        h.Selector.DidNotReceive().Resolve(Arg.Any<string>());
    }

    // ---- Admin protection enforced before persist (invariant #3) --------------------------------

    [Fact]
    public async Task DoNotUpdateVenue_IsSkippedEntirely_NothingPersisted()
    {
        var h = new Harness();
        GivenParsedMenu(h, SampleMenu());
        var mappedId = Guid.NewGuid();
        GivenNormalization(h, new NormalizationResult(
            new[] { new NormalizedMenuItem(mappedId, Uah(95m), "350 г") },
            Array.Empty<ParseQuarantineItem>()));
        GivenProtection(h, new RestaurantProtectionContextDto(Guid.NewGuid(), DoNotUpdate: true, new HashSet<Guid>()));

        var result = await h.Build().HandleAsync(Command());

        result.Outcome.Should().Be(RunParseOutcome.SkippedDoNotUpdate);
        await h.MenuWriter.DidNotReceive().PersistAsync(
            Arg.Any<ParsedRestaurantFactsDto>(), Arg.Any<IReadOnlyList<ResolvedMenuItemDto>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DoNotParseDish_IsExcludedFromTheWrite_OthersPersist()
    {
        var h = new Harness();
        GivenParsedMenu(h, SampleMenu());
        var protectedDish = Guid.NewGuid();
        var freeDish = Guid.NewGuid();
        GivenNormalization(h, new NormalizationResult(
            new[]
            {
                new NormalizedMenuItem(protectedDish, Uah(95m), "350 г"),
                new NormalizedMenuItem(freeDish, Uah(120m), "250 г"),
            },
            Array.Empty<ParseQuarantineItem>()));
        GivenProtection(h, new RestaurantProtectionContextDto(
            Guid.NewGuid(), DoNotUpdate: false, new HashSet<Guid> { protectedDish }));

        IReadOnlyList<ResolvedMenuItemDto>? written = null;
        await h.MenuWriter.PersistAsync(
            Arg.Any<ParsedRestaurantFactsDto>(),
            Arg.Do<IReadOnlyList<ResolvedMenuItemDto>>(items => written = items),
            Arg.Any<CancellationToken>());

        var result = await h.Build().HandleAsync(Command());

        result.Outcome.Should().Be(RunParseOutcome.Persisted);
        result.PersistedItemCount.Should().Be(1);
        result.DoNotParseSkippedCount.Should().Be(1);
        written.Should().NotBeNull();
        written!.Should().ContainSingle().Which.DishId.Should().Be(freeDish);
    }

    // ---- Unmappable -> quarantine, not dropped/fatal (invariant #2) ------------------------------

    [Fact]
    public async Task UnmappableDishes_AreQuarantined_WhileMappableStillPersist()
    {
        var h = new Harness();
        GivenParsedMenu(h, SampleMenu());
        var mappedId = Guid.NewGuid();
        var quarantine = new[]
        {
            ParseQuarantineItem.ForUnmapped("Борщ Кафе", "вул. Хрещатик, 1",
                new ParsedDish("Узвар", Uah(45m), "300 мл", "Напої"), DateTimeOffset.UtcNow),
        };
        GivenNormalization(h, new NormalizationResult(
            new[] { new NormalizedMenuItem(mappedId, Uah(95m), "350 г") },
            quarantine));
        GivenProtection(h, new RestaurantProtectionContextDto(null, DoNotUpdate: false, new HashSet<Guid>()));

        var result = await h.Build().HandleAsync(Command());

        result.Outcome.Should().Be(RunParseOutcome.Persisted);
        result.PersistedItemCount.Should().Be(1);
        result.QuarantinedItemCount.Should().Be(1);
        await h.Quarantine.Received(1).AddAsync(
            Arg.Is<IReadOnlyList<ParseQuarantineItem>>(q => q.Count == 1), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QuarantineHappens_EvenWhenVenueIsDoNotUpdate()
    {
        var h = new Harness();
        GivenParsedMenu(h, SampleMenu());
        var quarantine = new[]
        {
            ParseQuarantineItem.ForUnmapped("Борщ Кафе", "вул. Хрещатик, 1",
                new ParsedDish("Узвар", Uah(45m), null, null), DateTimeOffset.UtcNow),
        };
        GivenNormalization(h, new NormalizationResult(Array.Empty<NormalizedMenuItem>(), quarantine));
        GivenProtection(h, new RestaurantProtectionContextDto(Guid.NewGuid(), DoNotUpdate: true, new HashSet<Guid>()));

        var result = await h.Build().HandleAsync(Command());

        result.Outcome.Should().Be(RunParseOutcome.SkippedDoNotUpdate);
        result.QuarantinedItemCount.Should().Be(1);
        // Review data is written even though the live menu write is skipped (invariant #2 vs #3).
        await h.Quarantine.Received(1).AddAsync(Arg.Any<IReadOnlyList<ParseQuarantineItem>>(), Arg.Any<CancellationToken>());
    }
}
