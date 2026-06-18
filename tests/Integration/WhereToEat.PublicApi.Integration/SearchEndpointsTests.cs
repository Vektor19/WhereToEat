using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace WhereToEat.PublicApi.Integration;

/// <summary>
/// Endpoint-contract tests for the deterministic selection-building search (prefix list/lookup,
/// invariant #1 — no NLP). Asserts the prefix filter returns the seeded category/dish and that a
/// blank prefix yields an empty list (nothing to filter on).
/// </summary>
[Collection(PublicApiTestGroup.Name)]
public sealed class SearchEndpointsTests
{
    private readonly PublicApiFixture _fixture;

    public SearchEndpointsTests(PublicApiFixture fixture) => _fixture = fixture;

    private sealed record CategoryResponse(Guid Id, string Name);
    private sealed record DishResponse(Guid Id, Guid CategoryId, string CanonicalName);

    [Fact]
    public async Task SearchCategories_ByPrefix_ReturnsMatchingCategory()
    {
        var seed = await CatalogSeeder.SeedAsync(_fixture.ConnectionString);
        var client = _fixture.CreateClient();

        // "Перші страви" — prefix "Перш" must match it (anchored LIKE 'prefix%', not NLP).
        var response = await client.GetAsync(new Uri("/search/categories?prefix=Перш", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var categories = await response.Content.ReadFromJsonAsync<List<CategoryResponse>>();
        categories.Should().NotBeNull();
        categories!.Should().Contain(c => c.Id == seed.CategoryId);
    }

    [Fact]
    public async Task SearchDishes_ByPrefix_ReturnsMatchingDish()
    {
        var seed = await CatalogSeeder.SeedAsync(_fixture.ConnectionString);
        var client = _fixture.CreateClient();

        var response = await client.GetAsync(new Uri("/search/dishes?prefix=Борщ", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dishes = await response.Content.ReadFromJsonAsync<List<DishResponse>>();
        dishes.Should().NotBeNull();
        dishes!.Should().Contain(d => d.Id == seed.DishId);
    }

    [Fact]
    public async Task SearchDishes_BlankPrefix_ReturnsEmptyList()
    {
        var client = _fixture.CreateClient();

        var response = await client.GetAsync(new Uri("/search/dishes?prefix=", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dishes = await response.Content.ReadFromJsonAsync<List<DishResponse>>();
        dishes.Should().NotBeNull();
        dishes!.Should().BeEmpty();
    }
}
