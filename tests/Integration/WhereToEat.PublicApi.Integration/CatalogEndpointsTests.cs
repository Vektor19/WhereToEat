using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace WhereToEat.PublicApi.Integration;

/// <summary>
/// Endpoint-contract tests for the anonymous catalog reads: list categories, list dishes within a
/// category, and restaurant details — asserting details <b>always</b> carry the contact links (§5.8).
/// </summary>
[Collection(PublicApiTestGroup.Name)]
public sealed class CatalogEndpointsTests
{
    private readonly PublicApiFixture _fixture;

    public CatalogEndpointsTests(PublicApiFixture fixture) => _fixture = fixture;

    private sealed record CategoryResponse(Guid Id, string Name);
    private sealed record DishResponse(Guid Id, Guid CategoryId, string CanonicalName);
    private sealed record ContactLinkResponse(string Kind, string Url, string? Label);
    private sealed record MenuItemResponse(Guid Id, Guid DishId, decimal PriceAmount, string PriceCurrency, string? Weight);
    private sealed record DetailsResponse(
        Guid Id,
        string Name,
        string AddressLine,
        string? AddressCity,
        IReadOnlyList<ContactLinkResponse> ContactLinks,
        IReadOnlyList<MenuItemResponse> MenuItems);

    [Fact]
    public async Task ListCategories_ReturnsSeededCategory()
    {
        var seed = await CatalogSeeder.SeedAsync(_fixture.ConnectionString);
        var client = _fixture.CreateClient();

        var response = await client.GetAsync(new Uri("/categories", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var categories = await response.Content.ReadFromJsonAsync<List<CategoryResponse>>();
        categories.Should().NotBeNull();
        categories!.Should().Contain(c => c.Id == seed.CategoryId);
    }

    [Fact]
    public async Task ListDishesByCategory_ReturnsDishesInThatCategory()
    {
        var seed = await CatalogSeeder.SeedAsync(_fixture.ConnectionString);
        var client = _fixture.CreateClient();

        var response = await client.GetAsync(new Uri($"/categories/{seed.CategoryId}/dishes", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dishes = await response.Content.ReadFromJsonAsync<List<DishResponse>>();
        dishes.Should().NotBeNull();
        dishes!.Should().Contain(d => d.Id == seed.DishId && d.CategoryId == seed.CategoryId);
    }

    [Fact]
    public async Task GetRestaurantDetails_AlwaysIncludesContactLinks()
    {
        var seed = await CatalogSeeder.SeedAsync(_fixture.ConnectionString);
        var client = _fixture.CreateClient();

        var response = await client.GetAsync(new Uri($"/restaurants/{seed.RestaurantWithCoordsId}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var details = await response.Content.ReadFromJsonAsync<DetailsResponse>();
        details.Should().NotBeNull();
        // §5.8 / invariant #10: contact links are always present in details, even without Verified.
        details!.ContactLinks.Should().ContainSingle(l => l.Url == seed.ContactUrl);
        details.MenuItems.Should().ContainSingle(m => m.DishId == seed.DishId && m.PriceCurrency == "UAH");
    }

    [Fact]
    public async Task GetRestaurantDetails_ReturnsNotFound_ForUnknownRestaurant()
    {
        var client = _fixture.CreateClient();

        var response = await client.GetAsync(new Uri($"/restaurants/{Guid.NewGuid()}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
