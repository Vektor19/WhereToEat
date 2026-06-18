using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using WhereToEat.BuildingBlocks.Auth;
using WhereToEat.Contracts.Geo;
using WhereToEat.Geo.Application;
using WhereToEat.SharedKernel.Results;
using Xunit;

namespace WhereToEat.AdminApi.Integration;

/// <summary>
/// EditAddress raises the re-geocode flow (Step 10 acceptance): editing a restaurant's address through
/// the admin API persists the new address AND raises the Step 3 <c>AddressChanged</c> flow, which the
/// Step 8 re-geocode handler consumes (the seam lives in Step 8, dispatched here). The flow is made
/// observable by substituting the <see cref="IGeocoder"/> in the host's DI and asserting it was invoked
/// with the new address — proving the admin edit → AddressChanged → Step 8 geocode chain end-to-end.
/// A dedicated host with the substitute is built here (not the shared fixture's) so the substitution is
/// isolated to this test.
/// </summary>
[Collection(AdminApiTestGroup.Name)]
public sealed class EditAddressReGeocodeTests
{
    private readonly AdminApiFixture _fixture;

    public EditAddressReGeocodeTests(AdminApiFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task EditAddress_RaisesReGeocode_InvokingTheGeocoderWithTheNewAddress()
    {
        var seeder = new CatalogSeeder(_fixture.ConnectionString);
        var restaurantId = await seeder.AddRestaurantAsync("Geocode Diner", "вул. Стара, 1", "Київ");

        var geocoder = Substitute.For<IGeocoder>();
        geocoder.GeocodeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new GeoCoordinatesDto(50.45, 30.52, PlaceId: null, MapsDeepLink: null)));

        // A host that reuses the fixture's container/DB + test signing key, but swaps in the recording
        // geocoder so the re-geocode is observable without a live Nominatim call.
        using var factory = _fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddScoped(_ => geocoder)));

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.Admin());

        var response = await client.PutAsJsonAsync(
            new Uri($"/admin/restaurants/{restaurantId}/address", UriKind.Relative),
            new { addressLine = "вул. Хрещатик, 1", city = "Київ" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // The address is persisted...
        var address = await seeder.GetAddressAsync(restaurantId);
        address.AddressLine.Should().Be("вул. Хрещатик, 1");

        // ...and the Step 8 re-geocode flow ran: the geocoder was invoked with the NEW address.
        await geocoder.Received(1).GeocodeAsync(
            Arg.Is<string>(a => a.Contains("Хрещатик", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
    }
}
