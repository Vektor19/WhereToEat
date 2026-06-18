using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WhereToEat.Parsing.Domain;
using WhereToEat.Parsing.Infrastructure.Examples;
using WhereToEat.Parsing.Infrastructure.Strategies;
using WhereToEat.Parsing.Integration.Fixtures;
using Xunit;

namespace WhereToEat.Parsing.Integration;

/// <summary>
/// Step 9 library-parser integration test: the AngleSharp strategy (via the keyed
/// <see cref="PizzaHouseAngleSharpParser"/> example) parses the pinned static-HTML fixture over a
/// <c>file://</c> URL and produces the <b>same normalized <see cref="ParsedMenu"/> contract</b> the
/// Selenium e2e asserts — restaurant facts, contact links, and dish lines with prices as <c>Money</c>.
/// No browser, no Docker — pure in-process parse of the pinned fixture (CI-stable).
/// </summary>
public sealed class PizzaHouseParserTests
{
    private static PizzaHouseAngleSharpParser BuildParser()
        => new(new AngleSharpParserStrategy(NullLogger<AngleSharpParserStrategy>.Instance));

    [Fact]
    public async Task Parses_TheNormalizedMenuContract_FromTheStaticFixture()
    {
        var parser = BuildParser();
        var source = new SourceDescriptor(
            "Pizza House",
            FixtureFile.Url("pizza-house/menu.html"),
            PizzaHouseAngleSharpParser.Key);

        var result = await parser.ParseAsync(source);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Message : "the fixture parses");
        var menu = result.Value;

        // Restaurant facts.
        menu.Restaurant.Name.Should().Be("Pizza House");
        menu.Restaurant.AddressLine.Should().Be("вул. Сагайдачного, 12");
        menu.Restaurant.City.Should().Be("Київ");

        // Always-shown contact links (invariant #10).
        menu.Restaurant.ContactLinks.Should().Contain(l =>
            l.Kind == ParsedContactLinkKind.Website && l.Url == "https://pizza-house.example");
        menu.Restaurant.ContactLinks.Should().Contain(l =>
            l.Kind == ParsedContactLinkKind.Social && l.Url == "https://t.me/pizzahouse");

        // Dish lines with prices parsed to Money (units/currency unified by the strategy).
        menu.Dishes.Should().HaveCount(2);
        var margherita = menu.Dishes.Single(d => d.RawName == "Піца Маргарита");
        margherita.Price.Amount.Should().Be(180m);
        margherita.Price.Currency.Should().Be("UAH");
        margherita.Weight.Should().Be("450 г");
        margherita.CategoryHint.Should().Be("Фаст-фуд");

        var fries = menu.Dishes.Single(d => d.RawName == "Картопля фрі");
        fries.Price.Amount.Should().Be(75m);
    }
}
