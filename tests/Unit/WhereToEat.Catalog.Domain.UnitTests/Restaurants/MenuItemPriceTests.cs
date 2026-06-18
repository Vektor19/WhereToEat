using FluentAssertions;
using WhereToEat.Catalog.Domain.Identifiers;
using WhereToEat.Catalog.Domain.Restaurants;
using WhereToEat.SharedKernel.ValueObjects;
using Xunit;

namespace WhereToEat.Catalog.Domain.UnitTests.Restaurants;

/// <summary>
/// Price/Money guard (Step 3): a menu item's price is a non-negative <see cref="Money"/>. The
/// non-negativity is enforced by <see cref="Money"/> itself, so a negative price can never reach a
/// <see cref="MenuItem"/> — there is no way to construct the <see cref="Money"/> in the first place.
/// </summary>
public sealed class MenuItemPriceTests
{
    [Fact]
    public void Money_RejectsNegativePrice_SoMenuItemCanNeverHoldOne()
    {
        var negative = Money.Create(-1m, "UAH");

        negative.IsFailure.Should().BeTrue();
        negative.Error.Code.Should().Be("Money.NegativeAmount");
    }

    [Fact]
    public void AddMenuItem_WithZeroPrice_IsAllowed()
    {
        var restaurant = Restaurant.Create("Кафе", Address.Create("вул. Тестова, 1").Value).Value;
        var freebie = Money.Create(0m, "UAH").Value;

        var result = restaurant.AddMenuItem(DishId.New(), freebie);

        result.IsSuccess.Should().BeTrue();
        result.Value.Price.Amount.Should().Be(0m);
    }

    [Fact]
    public void AddMenuItem_WithValidPrice_StoresIt()
    {
        var restaurant = Restaurant.Create("Кафе", Address.Create("вул. Тестова, 1").Value).Value;
        var price = Money.Create(149.50m, "UAH").Value;

        var result = restaurant.AddMenuItem(DishId.New(), price, "300 г", SourceKind.Parsed);

        result.IsSuccess.Should().BeTrue();
        result.Value.Price.Should().Be(price);
        result.Value.Source.Should().Be(SourceKind.Parsed);
        result.Value.DoNotParse.Should().BeFalse();
    }
}
