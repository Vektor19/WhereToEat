using System.Net;
using System.Net.Http.Json;
using Dapper;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Xunit;

namespace WhereToEat.PublicApi.Integration;

/// <summary>
/// Analytics ingest-endpoint tests (Step 11 / §8.1). They drive <c>POST /analytics/events</c> through
/// the real host: a raw event with a user id + precise coordinates is accepted (202) and persists
/// <b>only</b> the anonymized shape — the raw id / precise coordinate never reach the stored row
/// (invariant #11). Bad input (unknown kind / out-of-range coordinate) returns 400, not 500.
/// </summary>
[Collection(PublicApiTestGroup.Name)]
public sealed class AnalyticsEndpointTests
{
    private readonly PublicApiFixture _fixture;

    public AnalyticsEndpointTests(PublicApiFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Ingest_AcceptsAndPersists_OnlyTheAnonymizedShape()
    {
        var restaurant = Guid.NewGuid();
        var client = _fixture.CreateClient();

        var response = await client.PostAsJsonAsync(
            new Uri("/analytics/events", UriKind.Relative),
            new
            {
                events = new[]
                {
                    new
                    {
                        kind = "CardOpen",
                        restaurantId = restaurant,
                        sortMode = "best",
                        userId = "endpoint-secret-user",
                        latitude = 50.4501234,
                        longitude = 30.5234123,
                    },
                },
            });

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        using var connection = new SqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();

        var row = await connection.QuerySingleAsync<(string? ActorHash, string? CoarseGeohash, string DimensionsJson)>(
            """
            SELECT TOP 1 ActorHash, CoarseGeohash, DimensionsJson
            FROM analytics.AnalyticsEvent
            WHERE CAST(JSON_VALUE(DimensionsJson, '$.restaurantId') AS UNIQUEIDENTIFIER) = @Restaurant;
            """,
            new { Restaurant = restaurant });

        // Hashed, not raw; coarse, not precise — the raw id / precise coordinate are gone.
        row.ActorHash.Should().HaveLength(64).And.NotContain("endpoint-secret-user");
        row.CoarseGeohash.Should().NotBeNullOrEmpty();
        row.CoarseGeohash!.Length.Should().BeLessThanOrEqualTo(6);
        row.DimensionsJson.Should().NotContain("endpoint-secret-user").And.NotContain("50.45").And.NotContain("30.52");
    }

    [Fact]
    public async Task Ingest_UnknownKind_Returns400()
    {
        var client = _fixture.CreateClient();

        var response = await client.PostAsJsonAsync(
            new Uri("/analytics/events", UriKind.Relative),
            new { events = new[] { new { kind = "not-a-kind" } } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Ingest_OutOfRangeCoordinate_Returns400()
    {
        var client = _fixture.CreateClient();

        var response = await client.PostAsJsonAsync(
            new Uri("/analytics/events", UriKind.Relative),
            new { events = new[] { new { kind = "Search", latitude = 999.0, longitude = 0.0 } } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Ingest_EmptyBatch_Returns400()
    {
        var client = _fixture.CreateClient();

        var response = await client.PostAsJsonAsync(
            new Uri("/analytics/events", UriKind.Relative),
            new { events = Array.Empty<object>() });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
