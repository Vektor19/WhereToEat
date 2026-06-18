using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace WhereToEat.PublicApi.Integration;

/// <summary>
/// Map-endpoint tests (§5.7). The payload must be assembled <b>only</b> from the restaurant's
/// stored DB fields (our OSM coordinates + Place ID + deep-link), with no geocoder involved (there
/// is none to invoke this step) and no Google rating/coordinate. A restaurant without stored
/// coordinates returns the "no map payload" shape; a missing restaurant returns 404.
/// </summary>
[Collection(PublicApiTestGroup.Name)]
public sealed class MapEndpointTests
{
    private readonly PublicApiFixture _fixture;

    public MapEndpointTests(PublicApiFixture fixture) => _fixture = fixture;

    private sealed record MapResponse(
        Guid RestaurantId,
        bool HasMapData,
        double? Latitude,
        double? Longitude,
        string? PlaceId,
        string? MapsDeepLink);

    [Fact]
    public async Task Map_EchoesOnlyStoredFields_ForRestaurantWithCoordinates()
    {
        var seed = await CatalogSeeder.SeedAsync(_fixture.ConnectionString);
        var client = _fixture.CreateClient();

        var response = await client.GetAsync(new Uri($"/map/{seed.RestaurantWithCoordsId}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<MapResponse>();
        payload.Should().NotBeNull();
        payload!.HasMapData.Should().BeTrue();
        payload.RestaurantId.Should().Be(seed.RestaurantWithCoordsId);
        // The stored OSM coordinate round-trips through the geography column within tolerance.
        payload.Latitude.Should().BeApproximately(seed.Latitude, 1e-5);
        payload.Longitude.Should().BeApproximately(seed.Longitude, 1e-5);
        payload.PlaceId.Should().Be(seed.PlaceId);
        payload.MapsDeepLink.Should().Be(seed.MapsDeepLink);

        // Nothing Google-rating-shaped is ever in the payload — the contract has no such field.
        var raw = await response.Content.ReadAsStringAsync();
        raw.Should().NotContain("rating", "the Map payload must never carry a Google rating (§5.7/#6)");
    }

    [Fact]
    public async Task Map_ReturnsNoMapData_ForRestaurantWithoutCoordinates()
    {
        var seed = await CatalogSeeder.SeedAsync(_fixture.ConnectionString);
        var client = _fixture.CreateClient();

        var response = await client.GetAsync(new Uri($"/map/{seed.RestaurantWithoutCoordsId}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<MapResponse>();
        payload.Should().NotBeNull();
        // No stored coordinates → the well-defined "no map payload"; we never geocode here.
        payload!.HasMapData.Should().BeFalse();
        payload.Latitude.Should().BeNull();
        payload.Longitude.Should().BeNull();
        payload.PlaceId.Should().BeNull();
        payload.MapsDeepLink.Should().BeNull();
    }

    [Fact]
    public async Task Map_ReturnsNotFound_ForUnknownRestaurant()
    {
        var client = _fixture.CreateClient();

        var response = await client.GetAsync(new Uri($"/map/{Guid.NewGuid()}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
