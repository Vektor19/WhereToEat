using Dapper;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using WhereToEat.BuildingBlocks.Persistence;
using WhereToEat.Catalog.Domain.Identifiers;
using WhereToEat.Catalog.Domain.Restaurants;
using WhereToEat.Catalog.Infrastructure.Persistence;
using WhereToEat.Contracts.Geo;
using WhereToEat.Contracts.IntegrationEvents;
using WhereToEat.Geo.Application;
using WhereToEat.SharedKernel.Results;
using Xunit;

namespace WhereToEat.Geo.Integration;

/// <summary>
/// Step 8 containerized-SQL integration test: the geocode write path end-to-end against a real SQL
/// Server. A stub <see cref="IGeocoder"/> (no live Nominatim) feeds an OSM coordinate; the real
/// <see cref="CatalogCoordinateWriter"/> (the Contracts seam) persists it through the Dapper
/// repository into the <c>geography</c> column; and the AddressChanged -> re-geocode handler is shown
/// to refresh the stored coordinate. There is no Google lat/lng anywhere in the path (invariant #7).
/// </summary>
public sealed class GeocodePersistenceTests : IClassFixture<SqlServerFixture>
{
    private const double KyivLat = 50.4501;
    private const double KyivLon = 30.5234;
    private const double LvivLat = 49.8397;
    private const double LvivLon = 24.0297;

    private readonly SqlConnectionFactory _connectionFactory;
    private readonly DapperCatalogRepository _repository;
    private readonly CatalogCoordinateWriter _coordinateWriter;

    public GeocodePersistenceTests(SqlServerFixture fixture)
    {
        _connectionFactory = new SqlConnectionFactory(fixture.ConnectionString);
        _repository = new DapperCatalogRepository(_connectionFactory);
        _coordinateWriter = new CatalogCoordinateWriter(_repository, _connectionFactory);
    }

    [Fact]
    public async Task GeocodeCommand_PersistsTheCoordinate_AsTheGeographyColumn()
    {
        var restaurantId = await SeedRestaurantWithoutCoordinatesAsync();

        var handler = new GeocodeRestaurantCommandHandler(
            new StubGeocoder(new GeoCoordinatesDto(KyivLat, KyivLon)),
            _coordinateWriter,
            NullLogger<GeocodeRestaurantCommandHandler>.Instance);

        var result = await handler.HandleAsync(new GeocodeRestaurantCommand(restaurantId.Value));

        result.IsSuccess.Should().BeTrue();

        // The coordinate is read back from the geography column as the OSM-sourced point.
        var loaded = await _repository.GetRestaurantByIdAsync(restaurantId);
        loaded!.Coordinates.Should().NotBeNull();
        loaded.Coordinates!.Point.Latitude.Should().BeApproximately(KyivLat, 1e-5);
        loaded.Coordinates.Point.Longitude.Should().BeApproximately(KyivLon, 1e-5);

        // The stored column's SQL type is geography (not a raw lat/lng pair).
        await using var connection = (SqlConnection)await _connectionFactory.CreateOpenConnectionAsync();
        var columnType = await connection.QuerySingleAsync<string>(
            """
            SELECT t.name
            FROM sys.columns c
            JOIN sys.types t ON c.user_type_id = t.user_type_id
            WHERE c.object_id = OBJECT_ID('catalog.Restaurant') AND c.name = 'Location';
            """);
        columnType.Should().Be("geography");

        // The persisted geography value's lat/lng round-trips through SQL itself.
        var storedLat = await connection.QuerySingleAsync<double>(
            "SELECT Location.Lat FROM catalog.Restaurant WHERE Id = @Id;",
            new { Id = restaurantId.Value });
        storedLat.Should().BeApproximately(KyivLat, 1e-5);
    }

    [Fact]
    public async Task AddressChangedHandler_RefreshesTheStoredCoordinate()
    {
        var restaurantId = await SeedRestaurantWithoutCoordinatesAsync();

        // First geocode -> Kyiv.
        var commandHandler = new GeocodeRestaurantCommandHandler(
            new StubGeocoder(new GeoCoordinatesDto(KyivLat, KyivLon)),
            _coordinateWriter,
            NullLogger<GeocodeRestaurantCommandHandler>.Instance);
        (await commandHandler.HandleAsync(new GeocodeRestaurantCommand(restaurantId.Value))).IsSuccess.Should().BeTrue();

        // Address changed -> re-geocode to a different (Lviv) coordinate via the seam.
        var addressChangedHandler = new GeocodeOnAddressChangedHandler(
            new GeocodeRestaurantCommandHandler(
                new StubGeocoder(new GeoCoordinatesDto(LvivLat, LvivLon)),
                _coordinateWriter,
                NullLogger<GeocodeRestaurantCommandHandler>.Instance),
            NullLogger<GeocodeOnAddressChangedHandler>.Instance);

        var @event = new RestaurantAddressChanged(Guid.NewGuid(), DateTimeOffset.UtcNow, restaurantId.Value);
        (await addressChangedHandler.HandleAsync(@event)).IsSuccess.Should().BeTrue();

        var loaded = await _repository.GetRestaurantByIdAsync(restaurantId);
        loaded!.Coordinates!.Point.Latitude.Should().BeApproximately(LvivLat, 1e-5);
        loaded.Coordinates.Point.Longitude.Should().BeApproximately(LvivLon, 1e-5);
    }

    [Fact]
    public async Task GeocodeCommand_UnknownRestaurant_ReturnsFailure()
    {
        var handler = new GeocodeRestaurantCommandHandler(
            new StubGeocoder(new GeoCoordinatesDto(KyivLat, KyivLon)),
            _coordinateWriter,
            NullLogger<GeocodeRestaurantCommandHandler>.Instance);

        var result = await handler.HandleAsync(new GeocodeRestaurantCommand(Guid.NewGuid()));

        result.IsFailure.Should().BeTrue();
    }

    private async Task<RestaurantId> SeedRestaurantWithoutCoordinatesAsync()
    {
        var address = Address.Create("вул. Хрещатик, 1", "Київ").Value;
        var restaurant = Restaurant.Create($"Заклад {Guid.NewGuid():N}", address).Value;
        await _repository.AddRestaurantAsync(restaurant);
        return restaurant.Id;
    }

    /// <summary>A stand-in <see cref="IGeocoder"/> returning a pinned OSM coordinate (no live calls).</summary>
    private sealed class StubGeocoder : IGeocoder
    {
        private readonly GeoCoordinatesDto _coordinate;

        public StubGeocoder(GeoCoordinatesDto coordinate) => _coordinate = coordinate;

        public Task<Result<GeoCoordinatesDto>> GeocodeAsync(string address, CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Success(_coordinate));
    }
}
