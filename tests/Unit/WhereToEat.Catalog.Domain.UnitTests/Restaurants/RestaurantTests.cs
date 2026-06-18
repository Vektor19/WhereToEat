using System.Linq;
using FluentAssertions;
using WhereToEat.Catalog.Domain.Events;
using WhereToEat.Catalog.Domain.Identifiers;
using WhereToEat.Catalog.Domain.Restaurants;
using WhereToEat.SharedKernel.Results;
using WhereToEat.SharedKernel.ValueObjects;
using Xunit;

namespace WhereToEat.Catalog.Domain.UnitTests.Restaurants;

/// <summary>
/// Restaurant aggregate invariants (Step 3): one menu item per dish, price/Money guard, and the
/// <see cref="AddressChanged"/> domain event on address mutation.
/// </summary>
public sealed class RestaurantTests
{
    private static Address SampleAddress => Address.Create("вул. Хрещатик, 1", "Київ").Value;

    private static Money Uah(decimal amount) => Money.Create(amount, "UAH").Value;

    private static Restaurant NewRestaurant() => Restaurant.Create("Борщ Кафе", SampleAddress).Value;

    [Fact]
    public void AddMenuItem_FirstItemForDish_Succeeds_AndRaisesUpsertedEvent()
    {
        var restaurant = NewRestaurant();
        var dishId = DishId.New();

        var result = restaurant.AddMenuItem(dishId, Uah(95m), "300 г");

        result.IsSuccess.Should().BeTrue();
        restaurant.MenuItems.Should().ContainSingle().Which.DishId.Should().Be(dishId);
        restaurant.DomainEvents.OfType<MenuItemUpserted>().Should().ContainSingle()
            .Which.DishId.Should().Be(dishId);
    }

    [Fact]
    public void AddMenuItem_SecondItemForSameDish_Fails()
    {
        var restaurant = NewRestaurant();
        var dishId = DishId.New();
        restaurant.AddMenuItem(dishId, Uah(95m));

        var result = restaurant.AddMenuItem(dishId, Uah(120m));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Restaurant.DuplicateDish");
        result.Error.Type.Should().Be(ErrorType.Conflict);
        restaurant.MenuItems.Should().ContainSingle();
    }

    [Fact]
    public void AddMenuItem_TwoDifferentDishes_BothAdded()
    {
        var restaurant = NewRestaurant();

        restaurant.AddMenuItem(DishId.New(), Uah(95m)).IsSuccess.Should().BeTrue();
        restaurant.AddMenuItem(DishId.New(), Uah(50m)).IsSuccess.Should().BeTrue();

        restaurant.MenuItems.Should().HaveCount(2);
    }

    [Fact]
    public void AddMenuItem_WithMissingDish_Fails()
    {
        var restaurant = NewRestaurant();

        var result = restaurant.AddMenuItem(default, Uah(95m));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("MenuItem.DishRequired");
    }

    [Fact]
    public void ChangeAddress_ToDifferentAddress_RaisesAddressChanged()
    {
        var restaurant = NewRestaurant();
        var newAddress = Address.Create("вул. Володимирська, 20", "Київ").Value;

        var result = restaurant.ChangeAddress(newAddress);

        result.IsSuccess.Should().BeTrue();
        restaurant.Address.Should().Be(newAddress);
        var evt = restaurant.DomainEvents.OfType<AddressChanged>().Should().ContainSingle().Subject;
        evt.RestaurantId.Should().Be(restaurant.Id);
        evt.NewAddress.Should().Be(newAddress);
    }

    [Fact]
    public void ChangeAddress_ToSameAddress_RaisesNoEvent()
    {
        var restaurant = NewRestaurant();
        var sameAddress = Address.Create("вул. Хрещатик, 1", "Київ").Value;

        var result = restaurant.ChangeAddress(sameAddress);

        result.IsSuccess.Should().BeTrue();
        restaurant.DomainEvents.OfType<AddressChanged>().Should().BeEmpty();
    }

    [Fact]
    public void Create_WithBlankName_Fails()
    {
        var result = Restaurant.Create("  ", SampleAddress);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Restaurant.NameRequired");
    }

    [Fact]
    public void SetCoordinates_StoresOsmCoordinates()
    {
        var restaurant = NewRestaurant();
        var coords = Coordinates.FromOsmGeoPoint(GeoPoint.Create(50.45, 30.52).Value, "ChIJ").Value;

        restaurant.SetCoordinates(coords);

        restaurant.Coordinates.Should().Be(coords);
    }

    [Fact]
    public void AddContactLink_DuplicateLink_IsIgnored()
    {
        var restaurant = NewRestaurant();
        var link = ContactLink.Create(ContactLinkKind.Website, "https://borsch.example").Value;
        var sameLink = ContactLink.Create(ContactLinkKind.Website, "https://borsch.example", "Site").Value;

        restaurant.AddContactLink(link);
        restaurant.AddContactLink(sameLink);

        restaurant.ContactLinks.Should().ContainSingle();
    }
}
