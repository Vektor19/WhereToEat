using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dapper;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Xunit;

namespace WhereToEat.AdminApi.Integration;

/// <summary>
/// The Step 14 admin Monetization endpoints (grant/revoke Verified, create a labeled ad placement) are
/// authorized by the SAME admin-only policy as the rest of the admin host: an admin token reaches the
/// handler (2xx), an authenticated non-admin → 403, anonymous → 401. The happy paths also prove the
/// effect persists through the production Dapper repos over the SQL-script-owned <c>monetization</c>
/// schema, behind the no-op payment seam. Nothing here feeds organic ranking (invariant #10).
/// </summary>
[Collection(AdminApiTestGroup.Name)]
public sealed class MonetizationEndpointsTests
{
    private readonly AdminApiFixture _fixture;

    public MonetizationEndpointsTests(AdminApiFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task GrantVerified_Anonymous_IsUnauthorized()
    {
        var client = _fixture.CreateClient();

        var response = await client.PutAsJsonAsync(
            new Uri($"/admin/venues/{Guid.NewGuid()}/verified", UriKind.Relative),
            new { tier = 2 });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GrantVerified_NonAdmin_IsForbidden()
    {
        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.User());

        var response = await client.PutAsJsonAsync(
            new Uri($"/admin/venues/{Guid.NewGuid()}/verified", UriKind.Relative),
            new { tier = 2 });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RevokeVerified_Anonymous_IsUnauthorized()
    {
        var client = _fixture.CreateClient();

        var response = await client.DeleteAsync(
            new Uri($"/admin/venues/{Guid.NewGuid()}/verified", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RevokeVerified_NonAdmin_IsForbidden()
    {
        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.User());

        var response = await client.DeleteAsync(
            new Uri($"/admin/venues/{Guid.NewGuid()}/verified", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateAdPlacement_Anonymous_IsUnauthorized()
    {
        var client = _fixture.CreateClient();

        var response = await client.PostAsJsonAsync(
            new Uri($"/admin/venues/{Guid.NewGuid()}/ad-placements", UriKind.Relative),
            new
            {
                targetingKey = "pizza:kyiv",
                startsAt = DateTimeOffset.UtcNow,
                endsAt = DateTimeOffset.UtcNow.AddDays(30),
            });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateAdPlacement_NonAdmin_IsForbidden()
    {
        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.User());

        var response = await client.PostAsJsonAsync(
            new Uri($"/admin/venues/{Guid.NewGuid()}/ad-placements", UriKind.Relative),
            new
            {
                targetingKey = "pizza:kyiv",
                startsAt = DateTimeOffset.UtcNow,
                endsAt = DateTimeOffset.UtcNow.AddDays(30),
            });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GrantThenRevokeVerified_AsAdmin_TogglesTheStoredTier()
    {
        var venueId = Guid.NewGuid();
        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.Admin());

        // Grant Pro → 204, tier persisted as 2.
        var grant = await client.PutAsJsonAsync(
            new Uri($"/admin/venues/{venueId}/verified", UriKind.Relative),
            new { tier = 2 });
        grant.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ReadTierAsync(venueId)).Should().Be(2);

        // Revoke → 204, tier back to 0 (the freemium baseline; the real-photo gate closes).
        var revoke = await client.DeleteAsync(
            new Uri($"/admin/venues/{venueId}/verified", UriKind.Relative));
        revoke.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ReadTierAsync(venueId)).Should().Be(0);
    }

    [Fact]
    public async Task CreateAdPlacement_AsAdmin_PersistsALabeledSlot()
    {
        var venueId = Guid.NewGuid();
        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.Admin());

        var response = await client.PostAsJsonAsync(
            new Uri($"/admin/venues/{venueId}/ad-placements", UriKind.Relative),
            new
            {
                targetingKey = "pizza:kyiv",
                startsAt = DateTimeOffset.UtcNow,
                endsAt = DateTimeOffset.UtcNow.AddDays(30),
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await CountAdPlacementsAsync(venueId)).Should().Be(1);
    }

    private async Task<int> ReadTierAsync(Guid venueId)
    {
        await using var connection = new SqlConnection(_fixture.ConnectionString);
        return await connection.QuerySingleOrDefaultAsync<int?>(
            "SELECT Tier FROM monetization.VerifiedStatus WHERE VenueId = @VenueId",
            new { VenueId = venueId }) ?? 0;
    }

    private async Task<int> CountAdPlacementsAsync(Guid venueId)
    {
        await using var connection = new SqlConnection(_fixture.ConnectionString);
        return await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM monetization.AdPlacement WHERE VenueId = @VenueId",
            new { VenueId = venueId });
    }
}
