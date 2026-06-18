using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WhereToEat.Contracts.Geo;
using WhereToEat.Contracts.IntegrationEvents;
using WhereToEat.Geo.Application;
using WhereToEat.SharedKernel.Results;
using Xunit;

namespace WhereToEat.Geo.UnitTests;

/// <summary>
/// Step 8 handler tests for the geocode command and the AddressChanged -> re-geocode seam, with a
/// mocked <see cref="IGeocoder"/> and a mocked <see cref="ICatalogCoordinateWriter"/> (the Contracts
/// seam). They prove the command reads the address, geocodes it, and persists the OSM coordinate
/// back — and that an AddressChanged event drives that same flow so stored coordinates are refreshed.
/// </summary>
public sealed class GeocodeHandlerTests
{
    private static readonly GeoCoordinatesDto KyivCoordinate = new(50.4501, 30.5234);

    private readonly IGeocoder _geocoder = Substitute.For<IGeocoder>();
    private readonly ICatalogCoordinateWriter _coordinateWriter = Substitute.For<ICatalogCoordinateWriter>();

    private GeocodeRestaurantCommandHandler CommandHandler() =>
        new(_geocoder, _coordinateWriter, NullLogger<GeocodeRestaurantCommandHandler>.Instance);

    [Fact]
    public async Task CommandHandler_GeocodesTheAddress_AndPersistsTheCoordinate()
    {
        var restaurantId = Guid.NewGuid();
        _coordinateWriter.GetGeocodingAddressAsync(restaurantId, Arg.Any<CancellationToken>())
            .Returns("вул. Хрещатик, 1, Київ");
        _geocoder.GeocodeAsync("вул. Хрещатик, 1, Київ", Arg.Any<CancellationToken>())
            .Returns(Result.Success(KyivCoordinate));
        _coordinateWriter.SetCoordinatesAsync(restaurantId, KyivCoordinate, Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await CommandHandler().HandleAsync(new GeocodeRestaurantCommand(restaurantId));

        result.IsSuccess.Should().BeTrue();
        await _coordinateWriter.Received(1).SetCoordinatesAsync(restaurantId, KyivCoordinate, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CommandHandler_UnknownRestaurant_ReturnsNotFound_AndDoesNotGeocode()
    {
        var restaurantId = Guid.NewGuid();
        _coordinateWriter.GetGeocodingAddressAsync(restaurantId, Arg.Any<CancellationToken>())
            .Returns((string?)null);

        var result = await CommandHandler().HandleAsync(new GeocodeRestaurantCommand(restaurantId));

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        await _geocoder.DidNotReceiveWithAnyArgs().GeocodeAsync(default!, default);
        await _coordinateWriter.DidNotReceiveWithAnyArgs().SetCoordinatesAsync(default, default!, default);
    }

    [Fact]
    public async Task CommandHandler_GeocodeFailure_PropagatesTheError_AndDoesNotPersist()
    {
        var restaurantId = Guid.NewGuid();
        _coordinateWriter.GetGeocodingAddressAsync(restaurantId, Arg.Any<CancellationToken>())
            .Returns("somewhere");
        _geocoder.GeocodeAsync("somewhere", Arg.Any<CancellationToken>())
            .Returns(Result.Failure<GeoCoordinatesDto>(Error.NotFound("Geocode.NoMatch", "no match")));

        var result = await CommandHandler().HandleAsync(new GeocodeRestaurantCommand(restaurantId));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Geocode.NoMatch");
        await _coordinateWriter.DidNotReceiveWithAnyArgs().SetCoordinatesAsync(default, default!, default);
    }

    [Fact]
    public async Task CommandHandler_WriteRejected_ReturnsFailure()
    {
        var restaurantId = Guid.NewGuid();
        _coordinateWriter.GetGeocodingAddressAsync(restaurantId, Arg.Any<CancellationToken>())
            .Returns("somewhere");
        _geocoder.GeocodeAsync("somewhere", Arg.Any<CancellationToken>())
            .Returns(Result.Success(KyivCoordinate));
        _coordinateWriter.SetCoordinatesAsync(restaurantId, KyivCoordinate, Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await CommandHandler().HandleAsync(new GeocodeRestaurantCommand(restaurantId));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Geocode.WriteRejected");
    }

    [Fact]
    public async Task AddressChangedHandler_TriggersTheGeocode_AndRefreshesTheStoredCoordinate()
    {
        var restaurantId = Guid.NewGuid();
        _coordinateWriter.GetGeocodingAddressAsync(restaurantId, Arg.Any<CancellationToken>())
            .Returns("вул. Нова, 9, Львів");
        _geocoder.GeocodeAsync("вул. Нова, 9, Львів", Arg.Any<CancellationToken>())
            .Returns(Result.Success(KyivCoordinate));
        _coordinateWriter.SetCoordinatesAsync(restaurantId, KyivCoordinate, Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = new GeocodeOnAddressChangedHandler(
            CommandHandler(),
            NullLogger<GeocodeOnAddressChangedHandler>.Instance);

        var @event = new RestaurantAddressChanged(Guid.NewGuid(), DateTimeOffset.UtcNow, restaurantId);
        var result = await handler.HandleAsync(@event);

        result.IsSuccess.Should().BeTrue();
        // The seam end-to-end: AddressChanged -> geocode -> stored coordinates refreshed.
        await _geocoder.Received(1).GeocodeAsync("вул. Нова, 9, Львів", Arg.Any<CancellationToken>());
        await _coordinateWriter.Received(1).SetCoordinatesAsync(restaurantId, KyivCoordinate, Arg.Any<CancellationToken>());
    }
}
