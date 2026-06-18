using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WhereToEat.Geo.Infrastructure;
using WhereToEat.SharedKernel.Results;
using Xunit;

namespace WhereToEat.Geo.UnitTests;

/// <summary>
/// Step 8 geocoder unit tests against a MOCKED HttpMessageHandler + a PINNED Nominatim JSON fixture
/// (no live Nominatim). They prove: a success maps to the OSM-sourced coordinate; the configured
/// User-Agent is sent; and an empty / error / out-of-range response returns a failure Result (never a
/// thrown exception, never a Google coordinate — invariant #7). The rate-limit is no longer the
/// geocoder's concern (it moved to the process-wide <see cref="NominatimRateLimiter"/> +
/// <see cref="RateLimitingHandler"/>); its deterministic tests live in
/// <see cref="NominatimRateLimiterTests"/>.
/// </summary>
public sealed class NominatimGeocoderTests
{
    // A pinned single-result jsonv2 response for Kyiv (Maidan), lat/lon as STRINGS as Nominatim sends.
    private const string KyivFixture =
        """
        [
          {
            "place_id": 12345,
            "lat": "50.4501",
            "lon": "30.5234",
            "display_name": "Майдан Незалежності, Київ, Україна"
          }
        ]
        """;

    private const string UserAgent = "WhereToEat-Test/1.0 (contact: test@wheretoeat.example)";

    private static HttpClient ClientFor(HttpMessageHandler handler)
    {
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://nominatim.test/") };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        return client;
    }

    [Fact]
    public async Task GeocodeAsync_MapsASuccessfulResponse_ToTheOsmSourcedCoordinate()
    {
        var handler = CountingMessageHandler.RespondingWith(KyivFixture);
        var geocoder = new NominatimGeocoder(ClientFor(handler), NullLogger<NominatimGeocoder>.Instance);

        var result = await geocoder.GeocodeAsync("Майдан Незалежності, Київ");

        result.IsSuccess.Should().BeTrue();
        result.Value.Latitude.Should().BeApproximately(50.4501, 1e-6);
        result.Value.Longitude.Should().BeApproximately(30.5234, 1e-6);
        // Nominatim provides no Google identifiers — they stay null (invariant #7: OSM point only).
        result.Value.PlaceId.Should().BeNull();
        result.Value.MapsDeepLink.Should().BeNull();
    }

    [Fact]
    public async Task GeocodeAsync_SendsTheDescriptiveUserAgent()
    {
        var handler = CountingMessageHandler.RespondingWith(KyivFixture);
        var geocoder = new NominatimGeocoder(ClientFor(handler), NullLogger<NominatimGeocoder>.Instance);

        await geocoder.GeocodeAsync("Майдан Незалежності, Київ");

        handler.RequestCount.Should().Be(1);
        handler.UserAgents.Single().Should().Contain("WhereToEat-Test");
    }

    [Fact]
    public async Task GeocodeAsync_EmptyResultArray_ReturnsAFailureResult()
    {
        var handler = CountingMessageHandler.RespondingWith("[]");
        var geocoder = new NominatimGeocoder(ClientFor(handler), NullLogger<NominatimGeocoder>.Instance);

        var result = await geocoder.GeocodeAsync("nowhere at all");

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Geocode.NoMatch");
    }

    [Fact]
    public async Task GeocodeAsync_UpstreamError_ReturnsAFailureResult()
    {
        var handler = new CountingMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var geocoder = new NominatimGeocoder(ClientFor(handler), NullLogger<NominatimGeocoder>.Instance);

        var result = await geocoder.GeocodeAsync("address");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Geocode.UpstreamError");
    }

    [Fact]
    public async Task GeocodeAsync_OutOfRangeCoordinate_ReturnsAFailureResult()
    {
        // A latitude of 999 is impossible — the SharedKernel GeoPoint guard must reject it so a
        // malformed upstream value can never become stored coordinates.
        const string badFixture = """[ { "lat": "999.0", "lon": "30.0" } ]""";
        var handler = CountingMessageHandler.RespondingWith(badFixture);
        var geocoder = new NominatimGeocoder(ClientFor(handler), NullLogger<NominatimGeocoder>.Instance);

        var result = await geocoder.GeocodeAsync("address");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("GeoPoint.LatitudeOutOfRange");
    }

    [Fact]
    public async Task GeocodeAsync_BlankAddress_ReturnsAValidationFailure_WithoutCallingTheService()
    {
        var handler = CountingMessageHandler.RespondingWith(KyivFixture);
        var geocoder = new NominatimGeocoder(ClientFor(handler), NullLogger<NominatimGeocoder>.Instance);

        var result = await geocoder.GeocodeAsync("   ");

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        handler.RequestCount.Should().Be(0, "a blank address must not reach the geocoding service");
    }
}
