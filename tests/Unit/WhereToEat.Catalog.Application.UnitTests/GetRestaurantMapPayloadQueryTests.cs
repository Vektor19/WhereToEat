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
/// Unit tests for <see cref="GetRestaurantMapPayloadQuery"/>: it builds the payload from stored
/// fields only (no geocoder — there is no such dependency), returns the "no map payload" shape when
/// the restaurant has no coordinates, and <c>null</c> when the restaurant is missing.
/// </summary>
public sealed class GetRestaurantMapPayloadQueryTests
{
    private readonly ICatalogRepository _repository = Substitute.For<ICatalogRepository>();

    [Fact]
    public async Task Execute_EchoesStoredCoordinatesAndIdentifiers()
    {
        var restaurant = CreateRestaurant();
        var point = GeoPoint.Create(50.4501, 30.5234).Value;
        var coordinates = Coordinates.FromOsmGeoPoint(point, "place-123", "https://maps.google.com/?cid=1").Value;
        restaurant.SetCoordinates(coordinates);

        _repository.GetRestaurantByIdAsync(restaurant.Id, Arg.Any<CancellationToken>()).Returns(restaurant);

        var query = new GetRestaurantMapPayloadQuery(_repository);
        var payload = await query.ExecuteAsync(restaurant.Id.Value);

        payload.Should().NotBeNull();
        payload!.HasMapData.Should().BeTrue();
        payload.Latitude.Should().Be(50.4501);
        payload.Longitude.Should().Be(30.5234);
        payload.PlaceId.Should().Be("place-123");
        payload.MapsDeepLink.Should().Be("https://maps.google.com/?cid=1");
    }

    [Fact]
    public async Task Execute_ReturnsNoMapData_WhenNoCoordinatesStored()
    {
        var restaurant = CreateRestaurant();
        _repository.GetRestaurantByIdAsync(restaurant.Id, Arg.Any<CancellationToken>()).Returns(restaurant);

        var query = new GetRestaurantMapPayloadQuery(_repository);
        var payload = await query.ExecuteAsync(restaurant.Id.Value);

        payload.Should().NotBeNull();
        payload!.HasMapData.Should().BeFalse();
        payload.Latitude.Should().BeNull();
        payload.Longitude.Should().BeNull();
        payload.PlaceId.Should().BeNull();
        payload.MapsDeepLink.Should().BeNull();
    }

    [Fact]
    public async Task Execute_ReturnsNull_WhenRestaurantMissing()
    {
        _repository.GetRestaurantByIdAsync(Arg.Any<RestaurantId>(), Arg.Any<CancellationToken>())
            .Returns((Restaurant?)null);

        var query = new GetRestaurantMapPayloadQuery(_repository);
        var payload = await query.ExecuteAsync(Guid.NewGuid());

        payload.Should().BeNull();
    }

    private static Restaurant CreateRestaurant()
    {
        var address = Address.Create("вул. Хрещатик, 1", "Київ").Value;
        return Restaurant.Create(RestaurantId.New(), "Борщ Хата", address).Value;
    }
}
