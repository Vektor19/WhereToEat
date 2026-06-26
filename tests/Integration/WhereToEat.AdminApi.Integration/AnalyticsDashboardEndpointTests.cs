using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace WhereToEat.AdminApi.Integration;

/// <summary>
/// Step 23 acceptance for the admin-scoped §7.5 analytics-dashboard read endpoints on the admin host:
/// the SAME admin-only policy as the other admin endpoints gates them (anonymous → 401, authenticated
/// non-admin → 403, admin → 200), the response is an aggregates-only DTO (counts/ratios, never a
/// per-user/per-event field), and a bad time window is a descriptive 400 (not a 500). The host now
/// registers the analytics READ path (AddAnalyticsReadModule) so IAnalyticsRollupReader resolves here.
/// </summary>
[Collection(AdminApiTestGroup.Name)]
public sealed class AnalyticsDashboardEndpointTests
{
    // A valid, small window so the only thing under test in the auth cases is the policy, not the window.
    private const string ValidWindow = "from=2026-08-01T00:00:00Z&to=2026-08-02T00:00:00Z";

    /// <summary>Identity fields that must never appear in an aggregates-only dashboard payload.</summary>
    private static readonly string[] IdentifyingJsonFields =
        ["actorHash", "userId", "sessionId", "latitude", "longitude", "geohash", "eventId"];

    private readonly AdminApiFixture _fixture;

    public AnalyticsDashboardEndpointTests(AdminApiFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Anonymous_IsUnauthorized()
    {
        var client = _fixture.CreateClient();

        var response = await client.GetAsync(new Uri(
            $"/admin/analytics/restaurants/{Guid.NewGuid()}/traffic?{ValidWindow}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AuthenticatedNonAdmin_IsForbidden()
    {
        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.User());

        var response = await client.GetAsync(new Uri(
            $"/admin/analytics/restaurants/{Guid.NewGuid()}/traffic?{ValidWindow}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_GetsTrafficAggregate_AggregatesOnly()
    {
        var client = AdminClient();

        var response = await client.GetAsync(new Uri(
            $"/admin/analytics/restaurants/{Guid.NewGuid()}/traffic?{ValidWindow}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        // A venue with no events is a zeroed aggregate — and the payload carries ONLY aggregate fields.
        json.GetProperty("impressions").GetInt64().Should().Be(0);
        json.GetProperty("cardOpens").GetInt64().Should().Be(0);
        json.GetProperty("actions").GetInt64().Should().Be(0);
        json.GetProperty("ctr").GetDouble().Should().Be(0d);

        var fields = json.EnumerateObject().Select(p => p.Name).ToList();
        fields.Should().NotContain(IdentifyingJsonFields);
    }

    [Fact]
    public async Task Admin_GetsConversionFunnelAggregate()
    {
        var client = AdminClient();

        var response = await client.GetAsync(new Uri(
            $"/admin/analytics/restaurants/{Guid.NewGuid()}/funnel?{ValidWindow}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("impressions").GetInt64().Should().Be(0);
        json.GetProperty("impressionToCardOpenRate").GetDouble().Should().Be(0d);
    }

    [Fact]
    public async Task Admin_GetsRatingsDistribution_ZeroedWhenUnrated()
    {
        var client = AdminClient();

        var response = await client.GetAsync(new Uri(
            $"/admin/analytics/restaurants/{Guid.NewGuid()}/ratings", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("ratingCount").GetInt64().Should().Be(0);
        json.GetProperty("averageScore").GetDouble().Should().Be(0d);
    }

    [Fact]
    public async Task Admin_GetsPricePositioning_EmptyForUnknownRestaurant()
    {
        var client = AdminClient();

        var response = await client.GetAsync(new Uri(
            $"/admin/analytics/restaurants/{Guid.NewGuid()}/price-positioning", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("dishes").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Admin_GetsDemandBreakdown()
    {
        var client = AdminClient();

        var response = await client.GetAsync(new Uri(
            $"/admin/analytics/demand?{ValidWindow}&top=10", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("categories").GetArrayLength().Should().Be(0);
        json.GetProperty("dishes").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Admin_MissingWindow_Is400_NotUnauthorizedOr500()
    {
        var client = AdminClient();

        var response = await client.GetAsync(new Uri(
            $"/admin/analytics/restaurants/{Guid.NewGuid()}/traffic", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("error").GetString().Should().Be("Analytics.Window.Required");
        json.GetProperty("message").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Admin_InvertedWindow_Is400()
    {
        var client = AdminClient();

        var response = await client.GetAsync(new Uri(
            $"/admin/analytics/restaurants/{Guid.NewGuid()}/traffic?from=2026-08-02T00:00:00Z&to=2026-08-01T00:00:00Z",
            UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("error").GetString().Should().Be("Analytics.Window.Inverted");
    }

    [Fact]
    public async Task Admin_OverlongWindow_Is400()
    {
        var client = AdminClient();

        var response = await client.GetAsync(new Uri(
            $"/admin/analytics/restaurants/{Guid.NewGuid()}/traffic?from=2024-01-01T00:00:00Z&to=2026-08-01T00:00:00Z",
            UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("error").GetString().Should().Be("Analytics.Window.TooLarge");
    }

    private HttpClient AdminClient()
    {
        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.Admin());
        return client;
    }
}
