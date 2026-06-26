using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dapper;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using WhereToEat.BuildingBlocks.Messaging.Consumers;
using WhereToEat.Contracts.IntegrationEvents;
using Xunit;

namespace WhereToEat.PublicApi.Integration;

/// <summary>
/// Step 22 rating-submit endpoint tests (§5.6 / invariant #6). They drive the authenticated
/// <c>POST /restaurants/{restaurantId}/ratings</c> through the real host: an anonymous request is 401,
/// a valid token + valid score returns 204 and persists exactly one raw <c>ratings.Rating</c> fact (the
/// use-case ran), a second submit by the same user <b>revises the same row</b> (never a duplicate — the
/// one-per-user-per-restaurant invariant), and an out-of-range score returns a descriptive 400
/// (never a 500). The opaque, derived user ref is what is stored — the raw IdP <c>sub</c> never lands
/// in the row (invariant #11). Tokens are minted with the fixture's test signing key (no live IdP).
/// </summary>
[Collection(PublicApiTestGroup.Name)]
public sealed class RatingEndpointTests
{
    private readonly PublicApiFixture _fixture;

    public RatingEndpointTests(PublicApiFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Submit_Returns401_WhenAnonymous()
    {
        var client = _fixture.CreateClient();

        var response = await client.PostAsJsonAsync(
            new Uri($"/restaurants/{Guid.NewGuid()}/ratings", UriKind.Relative),
            new { score = 4 });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Submit_Returns204_AndPersistsTheRawFact_WhenAuthenticatedAndValid()
    {
        var restaurant = Guid.NewGuid();
        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestJwt.Valid(subject: "rating-user-create", roles: "user"));

        var response = await client.PostAsJsonAsync(
            new Uri($"/restaurants/{restaurant}/ratings", UriKind.Relative),
            new { score = 5 });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var row = await ReadSingleRatingAsync(restaurant);
        row.Score.Should().Be(5, "the submit use-case ran and persisted the raw fact");
        row.UserId.Should().NotBe(Guid.Empty);
        // The stored ref is the opaque derived Guid — the raw subject is never persisted (invariant #11).
        row.UserId.ToString().Should().NotContain("rating-user-create");
    }

    [Fact]
    public async Task Submit_Twice_RevisesTheSameRow_WithoutDuplicating()
    {
        var restaurant = Guid.NewGuid();
        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestJwt.Valid(subject: "rating-user-revise", roles: "user"));

        var first = await client.PostAsJsonAsync(
            new Uri($"/restaurants/{restaurant}/ratings", UriKind.Relative),
            new { score = 2 });
        first.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // The same user revises: same row, new score, never a second row (UQ_Rating_Restaurant_User).
        var second = await client.PostAsJsonAsync(
            new Uri($"/restaurants/{restaurant}/ratings", UriKind.Relative),
            new { score = 4 });
        second.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var count = await CountRatingsAsync(restaurant);
        count.Should().Be(1, "a revise updates the user's single row, it never inserts a second");

        var row = await ReadSingleRatingAsync(restaurant);
        row.Score.Should().Be(4, "the revised score overwrote the original in place");
    }

    [Fact]
    public async Task Submit_Returns400_OnOutOfRangeScore()
    {
        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestJwt.Valid(subject: "rating-user-badscore", roles: "user"));

        var response = await client.PostAsJsonAsync(
            new Uri($"/restaurants/{Guid.NewGuid()}/ratings", UriKind.Relative),
            new { score = 6 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var payload = await response.Content.ReadFromJsonAsync<ErrorEnvelope>();
        payload.Should().NotBeNull();
        payload!.Error.Should().Be("Rating.ScoreOutOfRange");
        payload.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Submit_PublishesRatingGiven_OnSuccess()
    {
        var restaurant = Guid.NewGuid();

        // A host that reuses the fixture's container/DB + test signing key but swaps the real bus for
        // the MassTransit in-memory test harness so the RatingGiven publish is observable (no broker).
        // ConfigureTestServices runs after the host's own AddMessaging, so the swap wins. The harness is
        // dedicated to this factory — the shared fixture's host (and its other tests) are untouched.
        using var factory = _fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(WithRatingPublishHarness));

        var harness = factory.Services.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", TestJwt.Valid(subject: "rating-user-publish", roles: "user"));

            var response = await client.PostAsJsonAsync(
                new Uri($"/restaurants/{restaurant}/ratings", UriKind.Relative),
                new { score = 5 });

            response.StatusCode.Should().Be(HttpStatusCode.NoContent);

            // The submit use-case persisted the fact and the adapter published the integration event so
            // the recompute rebuilds the cumulative smoothed aggregate (Step 21 / invariant #6).
            (await harness.Published.Any<RatingGiven>(
                published => published.Context.Message.RestaurantId == restaurant)).Should().BeTrue(
                "a successful submit publishes RatingGiven for the rated restaurant");
        }
        finally
        {
            await harness.Stop();
        }
    }

    /// <summary>
    /// Replaces the host's MassTransit bus with the in-memory test harness so a publish can be observed.
    /// Every MassTransit-registered descriptor from the host's <c>AddMessaging</c> is removed first (so the
    /// harness's own bus wins, with no duplicate <c>IBusControl</c>), then the harness is added with the
    /// same two cross-cutting consumers re-registered (their host-provided dependencies still resolve).
    /// </summary>
    private static void WithRatingPublishHarness(IServiceCollection services)
    {
        var massTransitDescriptors = services
            .Where(d =>
                (d.ServiceType.Assembly.GetName().Name?.StartsWith("MassTransit", StringComparison.Ordinal) ?? false) ||
                (d.ImplementationType?.Assembly.GetName().Name?.StartsWith("MassTransit", StringComparison.Ordinal) ?? false))
            .ToList();
        foreach (var descriptor in massTransitDescriptors)
        {
            services.Remove(descriptor);
        }

        services.AddMassTransitTestHarness(cfg =>
        {
            cfg.AddConsumer<GeocodeOnAddressChangedConsumer>();
            cfg.AddConsumer<InvalidateCacheOnMenuUpdatedConsumer>();
        });
    }

    private async Task<(Guid UserId, int Score)> ReadSingleRatingAsync(Guid restaurant)
    {
        using var connection = new SqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();

        return await connection.QuerySingleAsync<(Guid UserId, int Score)>(
            "SELECT TOP 1 UserId, Score FROM ratings.Rating WHERE RestaurantId = @Restaurant;",
            new { Restaurant = restaurant });
    }

    private async Task<int> CountRatingsAsync(Guid restaurant)
    {
        using var connection = new SqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();

        return await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM ratings.Rating WHERE RestaurantId = @Restaurant;",
            new { Restaurant = restaurant });
    }

    private sealed record ErrorEnvelope(string Error, string Message);
}
