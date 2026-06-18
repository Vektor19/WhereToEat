using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WhereToEat.AdminApi.Integration.Parsing;
using WhereToEat.BuildingBlocks.Persistence;
using WhereToEat.Catalog.Infrastructure.Parsing;
using WhereToEat.Contracts.Geo;
using WhereToEat.Contracts.Parsing;
using WhereToEat.Parsing.Application;
using WhereToEat.Parsing.Application.Compliance;
using WhereToEat.Parsing.Domain;
using WhereToEat.Parsing.Infrastructure;
using WhereToEat.SharedKernel.ValueObjects;
using Xunit;

namespace WhereToEat.AdminApi.Integration;

/// <summary>
/// The END-TO-END protection test (Step 10 acceptance, cross-checks invariant #3 across modules): an
/// admin sets <c>DoNotUpdate</c> on one restaurant and <c>DoNotParse</c> on a menu item of another, via
/// the admin API; then the real Step 9 parse pipeline runs against fixture menus; and the protected
/// data is asserted UNTOUCHED — the DoNotUpdate venue gets no new items and the DoNotParse item keeps
/// its admin-curated price, while an UNprotected item in the same updatable venue is still overwritten
/// (proving the gate is selective, not a blanket skip).
/// </summary>
[Collection(AdminApiTestGroup.Name)]
public sealed class ProtectionSurvivesParseTests
{
    private readonly AdminApiFixture _fixture;

    public ProtectionSurvivesParseTests(AdminApiFixture fixture) => _fixture = fixture;

    private HttpClient AdminClient()
    {
        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.Admin());
        return client;
    }

    [Fact]
    public async Task DoNotUpdate_RestaurantIsSkippedEntirely_ByTheParse()
    {
        var seeder = new CatalogSeeder(_fixture.ConnectionString);
        var categoryId = await seeder.AddCategoryAsync("Перші страви (DoNotUpdate)");
        var borschDish = await seeder.AddDishAsync(categoryId, "Борщ (DoNotUpdate)");

        // A hand-curated venue with one admin item; the admin flags the WHOLE venue DoNotUpdate.
        var restaurantId = await seeder.AddRestaurantAsync("Curated Borsch", "вул. Захищена, 1");
        await seeder.AddMenuItemAsync(restaurantId, borschDish, 95m, "UAH", source: 1);

        var flagResponse = await AdminClient().PutAsJsonAsync(
            new Uri($"/admin/restaurants/{restaurantId}/do-not-update", UriKind.Relative),
            new { value = true });
        flagResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // A parse that, unprotected, would ADD a new dish to this venue.
        var newDish = await seeder.AddDishAsync(categoryId, "Солянка (new)");
        var menu = MenuFor(
            "Curated Borsch",
            "вул. Захищена, 1",
            ("Солянка (new)", 120m));

        var itemsBefore = await seeder.CountMenuItemsAsync(restaurantId);
        var outcome = await RunParseAsync(menu);

        // The whole venue is skipped (invariant #3): no new items, the curated price is untouched.
        outcome.Outcome.Should().Be(RunParseOutcome.SkippedDoNotUpdate);
        (await seeder.CountMenuItemsAsync(restaurantId)).Should().Be(itemsBefore);
        (await seeder.GetPriceAsync(restaurantId, borschDish)).Should().Be(95m);
        (await seeder.GetPriceAsync(restaurantId, newDish)).Should().BeNull("the DoNotUpdate venue got no new item");
    }

    [Fact]
    public async Task DoNotParse_ItemSurvives_WhileUnprotectedItemIsOverwritten()
    {
        var seeder = new CatalogSeeder(_fixture.ConnectionString);
        var categoryId = await seeder.AddCategoryAsync("Фаст-фуд (DoNotParse)");
        var protectedDish = await seeder.AddDishAsync(categoryId, "Піца Маргарита (protected)");
        var openDish = await seeder.AddDishAsync(categoryId, "Картопля фрі (open)");

        // An updatable venue with two admin items; the admin flags only ONE item DoNotParse.
        var restaurantId = await seeder.AddRestaurantAsync("Mixed Pizza", "вул. Змішана, 2");
        var protectedItemId = await seeder.AddMenuItemAsync(restaurantId, protectedDish, 200m, "UAH", source: 1);
        await seeder.AddMenuItemAsync(restaurantId, openDish, 70m, "UAH", source: 1);

        var flagResponse = await AdminClient().PutAsJsonAsync(
            new Uri($"/admin/menu-items/{protectedItemId}/do-not-parse", UriKind.Relative),
            new { value = true });
        flagResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // A parse that brings NEW prices for BOTH dishes.
        var menu = MenuFor(
            "Mixed Pizza",
            "вул. Змішана, 2",
            ("Піца Маргарита (protected)", 999m),
            ("Картопля фрі (open)", 85m));

        var outcome = await RunParseAsync(menu);

        outcome.Outcome.Should().Be(RunParseOutcome.Persisted);
        // The DoNotParse item keeps its admin price; the open item is overwritten by the parse.
        (await seeder.GetPriceAsync(restaurantId, protectedDish)).Should().Be(200m, "DoNotParse shields the item (invariant #3)");
        (await seeder.GetPriceAsync(restaurantId, openDish)).Should().Be(85m, "an unprotected item is still updated");
    }

    private static ParsedMenu MenuFor(string name, string addressLine, params (string Raw, decimal Price)[] dishes)
    {
        var facts = new ParsedRestaurantFacts(name, addressLine, "Київ", Array.Empty<ParsedContactLink>());
        var lines = dishes
            .Select(d => new ParsedDish(d.Raw, Money.Create(d.Price, "UAH").Value, null, null))
            .ToList();
        return new ParsedMenu(facts, lines);
    }

    /// <summary>
    /// Runs the real Step 9 <see cref="RunParseCommandHandler"/> with permissive compliance gates, a
    /// stub parser strategy (supplying the fixed menu), the real <see cref="Normalizer"/>, and the
    /// Dapper catalog seams reading/writing the SAME database the admin host edited — so the admin
    /// flags persisted via EF are exactly the ones the parser gate observes.
    /// </summary>
    private async Task<RunParseResult> RunParseAsync(ParsedMenu menu)
    {
        var allowList = Substitute.For<IFirstPartyAllowList>();
        allowList.IsFirstParty(Arg.Any<string>()).Returns(true);
        var robots = Substitute.For<IRobotsTxtGate>();
        robots.IsAllowedAsync(Arg.Any<Uri>(), Arg.Any<CancellationToken>()).Returns(true);
        var rateLimiter = Substitute.For<IHostRateLimiter>();
        rateLimiter.TryAcquireAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        var geocoder = Substitute.For<IRestaurantGeocoder>();
        geocoder.GeocodeAndStoreAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);

        // Use the PRODUCTION Catalog parsing adapters over a real connection factory bound to the
        // fixture DB — so the end-to-end protection test exercises the persist path that actually
        // ships (the gate reads/writes via the same adapters the host wires), not a test double.
        var connectionFactory = new SqlConnectionFactory(_fixture.ConnectionString);
        var strategy = new StubParserStrategy(menu);
        var dishResolver = new DapperCatalogDishResolver(connectionFactory);
        var normalizer = new Normalizer(dishResolver, TimeProvider.System, NullLogger<Normalizer>.Instance);
        var quarantineStore = Substitute.For<IParseQuarantineStore>();
        var menuWriter = new DapperCatalogMenuWriter(connectionFactory);

        var handler = new RunParseCommandHandler(
            allowList,
            robots,
            rateLimiter,
            strategy,
            normalizer,
            quarantineStore,
            menuWriter,
            geocoder,
            NullLogger<RunParseCommandHandler>.Instance);

        var source = new SourceDescriptor(menu.Restaurant.Name, new Uri("https://first-party.example/menu"), StubParserStrategy.Key);
        return await handler.HandleAsync(new RunParseCommand(source));
    }
}
