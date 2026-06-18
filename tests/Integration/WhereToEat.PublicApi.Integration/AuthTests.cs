using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Xunit;

namespace WhereToEat.PublicApi.Integration;

/// <summary>
/// OIDC resource-server auth behaviour. Public read endpoints stay anonymous (200 with no token),
/// while the authenticated seam (<c>/me</c>) rejects an unauthenticated/invalid token with 401 and
/// accepts a valid token. Tokens are minted with the fixture's test signing key (no live IdP).
/// </summary>
[Collection(PublicApiTestGroup.Name)]
public sealed class AuthTests
{
    private readonly PublicApiFixture _fixture;

    public AuthTests(PublicApiFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task PublicRead_IsReachable_WithoutToken()
    {
        var client = _fixture.CreateClient();

        var response = await client.GetAsync(new Uri("/categories", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ProtectedEndpoint_ReturnsUnauthorized_WithoutToken()
    {
        var client = _fixture.CreateClient();

        var response = await client.GetAsync(new Uri("/me", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_ReturnsUnauthorized_WithInvalidToken()
    {
        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "not-a-real-token");

        var response = await client.GetAsync(new Uri("/me", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_ReturnsUnauthorized_WithWellFormedTokenSignedByWrongKey()
    {
        // A structurally valid HS256 JWT, but signed with a key the host does not trust. A 401 here
        // proves the signature is actually validated — not merely that malformed strings are rejected.
        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwt.SignedWithUntrustedKey(subject: "user-1", roles: "user"));

        var response = await client.GetAsync(new Uri("/me", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_ReturnsOk_WithValidToken()
    {
        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestJwt.Valid(subject: "user-1", roles: "user"));

        var response = await client.GetAsync(new Uri("/me", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
