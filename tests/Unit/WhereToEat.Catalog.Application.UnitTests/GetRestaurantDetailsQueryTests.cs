using FluentAssertions;
using NSubstitute;
using WhereToEat.Catalog.Application.Queries;
using WhereToEat.Catalog.Domain.Abstractions;
using WhereToEat.Catalog.Domain.Identifiers;
using WhereToEat.Catalog.Domain.Restaurants;
using WhereToEat.SharedKernel.ValueObjects;
using Xunit;

namespace WhereToEat.Catalog.Application.UnitTests;

/// <summary>
/// Unit tests for <see cref="GetRestaurantDetailsQuery"/> with a mocked repository: details always
/// carry the contact links (§5.8) and a missing restaurant yields <c>null</c> (the host answers 404).
/// </summary>
public sealed class GetRestaurantDetailsQueryTests
{
    private readonly ICatalogRepository _repository = Substitute.For<ICatalogRepository>();

    [Fact]
    public async Task Execute_AlwaysReturnsContactLinks()
    {
        var address = Address.Create("вул. Хрещатик, 1", "Київ").Value;
        var restaurant = Restaurant.Create(RestaurantId.New(), "Борщ Хата", address).Value;
        var link = ContactLink.Create(ContactLinkKind.Website, "https://borsch.example.com", "Сайт").Value;
        restaurant.AddContactLink(link);
        var dishId = DishId.New();
        var menuItem = restaurant.AddMenuItem(dishId, Money.Create(120m, "UAH").Value, "350 г");
        menuItem.IsSuccess.Should().BeTrue();

        _repository.GetRestaurantByIdAsync(restaurant.Id, Arg.Any<CancellationToken>())
            .Returns(restaurant);

        var query = new GetRestaurantDetailsQuery(_repository);
        var details = await query.ExecuteAsync(restaurant.Id.Value);

        details.Should().NotBeNull();
        details!.ContactLinks.Should().ContainSingle(l => l.Url == "https://borsch.example.com");
        details.MenuItems.Should().ContainSingle(m => m.DishId == dishId.Value && m.PriceAmount == 120m);
    }

    [Fact]
    public async Task Execute_ReturnsNull_WhenRestaurantMissing()
    {
        _repository.GetRestaurantByIdAsync(Arg.Any<RestaurantId>(), Arg.Any<CancellationToken>())
            .Returns((Restaurant?)null);

        var query = new GetRestaurantDetailsQuery(_repository);
        var details = await query.ExecuteAsync(Guid.NewGuid());

        details.Should().BeNull();
    }
}
