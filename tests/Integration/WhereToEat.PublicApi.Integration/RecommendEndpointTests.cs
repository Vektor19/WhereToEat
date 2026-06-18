using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace WhereToEat.PublicApi.Integration;

/// <summary>
/// End-to-end <c>POST /recommend</c> over the real host + a Testcontainers SQL Server. It seeds the
/// rating aggregate AND the price-median table <b>directly</b> (so the test does not depend on the
/// Step 12 nightly/recompute jobs — median-data + rating independence) and asserts the end-to-end
/// ordering for one explicit mode (<c>price</c> — cheapest first, invariant #5) and one composite
/// mode (<c>price-quality</c> — the higher-rated venue wins the price/quality compromise). Also
/// asserts the request/response contract and the validation failures for bad keys.
/// </summary>
[Collection(PublicApiTestGroup.Name)]
public sealed class RecommendEndpointTests
{
    private readonly PublicApiFixture _fixture;

    public RecommendEndpointTests(PublicApiFixture fixture) => _fixture = fixture;

    private sealed record RecommendResponse(string Match, string Sort, IReadOnlyList<RecommendedRestaurant> Restaurants);

    private sealed record RecommendedRestaurant(
        Guid RestaurantId,
        string Name,
        decimal? BasketPriceAmount,
        string? BasketPriceCurrency,
        double? SmoothedRating,
        int RatingCount,
        double? DistanceKm,
        int Coverage);

    [Fact]
    public async Task Recommend_ExplicitPriceSort_RanksCheapestFirst()
    {
        var seed = await RecommendSeeder.SeedAsync(_fixture.ConnectionString);
        var client = _fixture.CreateClient();

        var body = new
        {
            items = new[] { new { categoryId = (Guid?)null, dishId = (Guid?)seed.DishId } },
            match = "or",
            sort = "price",
            filters = Array.Empty<object>(),
            userGeo = new { latitude = 50.4501, longitude = 30.5234 },
        };

        var response = await client.PostAsJsonAsync(new Uri("/recommend", UriKind.Relative), body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<RecommendResponse>();
        result.Should().NotBeNull();

        var ours = result!.Restaurants
            .Where(r => r.RestaurantId == seed.CheapLowRatedId || r.RestaurantId == seed.PriceyHighRatedId)
            .ToList();
        ours.Should().HaveCount(2);

        // Explicit price sort: the cheaper basket is first even though it is the lower-rated venue
        // (price dominates; coverage is only a tie-breaker — invariant #5).
        ours[0].RestaurantId.Should().Be(seed.CheapLowRatedId);
        ours[0].BasketPriceAmount.Should().Be(seed.CheapPrice);
        ours[0].BasketPriceCurrency.Should().Be("UAH");
        ours[1].RestaurantId.Should().Be(seed.PriceyHighRatedId);

        // The smoothed rating crossed the module boundary purely as a DTO field (no Ratings.Domain):
        // it is present and reflects the Bayesian-smoothed value, not the raw average.
        ours[1].SmoothedRating.Should().NotBeNull();
        ours[1].SmoothedRating!.Value.Should().BeGreaterThan(ours[0].SmoothedRating!.Value);
    }

    [Fact]
    public async Task Recommend_CompositePriceQuality_RanksHigherRatedVenueFirst()
    {
        var seed = await RecommendSeeder.SeedAsync(_fixture.ConnectionString);
        var client = _fixture.CreateClient();

        var body = new
        {
            items = new[] { new { categoryId = (Guid?)null, dishId = (Guid?)seed.DishId } },
            match = "or",
            sort = "price-quality",
            filters = Array.Empty<object>(),
            userGeo = new { latitude = 50.4501, longitude = 30.5234 },
        };

        var response = await client.PostAsJsonAsync(new Uri("/recommend", UriKind.Relative), body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<RecommendResponse>();

        var ours = result!.Restaurants
            .Where(r => r.RestaurantId == seed.CheapLowRatedId || r.RestaurantId == seed.PriceyHighRatedId)
            .ToList();
        ours.Should().HaveCount(2);

        // The composite price-quality blend lifts the markedly-higher-rated venue above the merely
        // cheaper one (the f_price median reference is seeded, not computed per request).
        ours[0].RestaurantId.Should().Be(seed.PriceyHighRatedId);
    }

    [Fact]
    public async Task Recommend_RejectsUnknownSortKey_With400()
    {
        var seed = await RecommendSeeder.SeedAsync(_fixture.ConnectionString);
        var client = _fixture.CreateClient();

        var body = new
        {
            items = new[] { new { categoryId = (Guid?)null, dishId = (Guid?)seed.DishId } },
            match = "or",
            sort = "no-such-mode",
            filters = Array.Empty<object>(),
            userGeo = (object?)null,
        };

        var response = await client.PostAsJsonAsync(new Uri("/recommend", UriKind.Relative), body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Recommend_RejectsEmptySelection_With400()
    {
        var client = _fixture.CreateClient();

        var body = new
        {
            items = Array.Empty<object>(),
            match = "or",
            sort = "price",
            filters = Array.Empty<object>(),
            userGeo = (object?)null,
        };

        var response = await client.PostAsJsonAsync(new Uri("/recommend", UriKind.Relative), body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Recommend_RejectsItemWithNeitherCategoryNorDish_With400()
    {
        var client = _fixture.CreateClient();

        // An item with both ids null must be a clean 400, not an unhandled 500 from the
        // SelectedItem factory (which throws on an empty id) — invariant #2 needs exactly one.
        var body = new
        {
            items = new[] { new { categoryId = (Guid?)null, dishId = (Guid?)null } },
            match = "or",
            sort = "price",
            filters = Array.Empty<object>(),
            userGeo = (object?)null,
        };

        var response = await client.PostAsJsonAsync(new Uri("/recommend", UriKind.Relative), body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Recommend_RejectsOutOfRangeUserGeo_With400()
    {
        var seed = await RecommendSeeder.SeedAsync(_fixture.ConnectionString);
        var client = _fixture.CreateClient();

        // An out-of-range latitude (91) must be a 400, not a silent drop of the distance ranking
        // the caller asked for (Issue 5 — no silent degradation).
        var body = new
        {
            items = new[] { new { categoryId = (Guid?)null, dishId = (Guid?)seed.DishId } },
            match = "or",
            sort = "distance",
            filters = Array.Empty<object>(),
            userGeo = new { latitude = 91.0, longitude = 30.5234 },
        };

        var response = await client.PostAsJsonAsync(new Uri("/recommend", UriKind.Relative), body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
