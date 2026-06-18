using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace WhereToEat.AdminApi.Integration;

/// <summary>
/// Admin-only authorization (Step 10 acceptance): the admin host validates the SAME OIDC tokens as the
/// public host but authorizes ONLY admin roles/scopes. An anonymous request → 401, an authenticated
/// non-admin (<c>user</c>) → 403, and an <c>admin</c> token reaches the handler (it is NOT rejected by
/// authentication/authorization — a 401/403 here would be the failure). This is the same
/// <c>AdminAuthorization</c> policy Step 13 later exercises against the dev Keycloak realm.
/// </summary>
[Collection(AdminApiTestGroup.Name)]
public sealed class AdminAuthorizationTests
{
    private readonly AdminApiFixture _fixture;

    public AdminAuthorizationTests(AdminApiFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Anonymous_IsUnauthorized()
    {
        var client = _fixture.CreateClient();

        var response = await client.PutAsJsonAsync(
            new Uri($"/admin/restaurants/{Guid.NewGuid()}/do-not-update", UriKind.Relative),
            new { value = true });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AuthenticatedNonAdmin_IsForbidden()
    {
        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.User());

        var response = await client.PutAsJsonAsync(
            new Uri($"/admin/restaurants/{Guid.NewGuid()}/do-not-update", UriKind.Relative),
            new { value = true });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_IsAuthorized_AndReachesHandler()
    {
        // Seed a real restaurant so the admin write succeeds with 204 (proving the admin token passes
        // authorization AND the handler ran — not a 401/403).
        var seeder = new CatalogSeeder(_fixture.ConnectionString);
        var restaurantId = await seeder.AddRestaurantAsync("Authz Diner", "вул. Тестова, 1");

        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.Admin());

        var response = await client.PutAsJsonAsync(
            new Uri($"/admin/restaurants/{restaurantId}/do-not-update", UriKind.Relative),
            new { value = true });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
