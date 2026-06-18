using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using WhereToEat.BuildingBlocks.Auth;
using Xunit;

namespace WhereToEat.CrossCutting.Integration;

/// <summary>
/// The Step 13 Keycloak realm / authorization gate. Two parts, neither needing a live Keycloak:
/// <list type="number">
///   <item>a <b>file assertion</b> that the dev realm export (<c>deploy/keycloak/realm-export.json</c>)
///   defines BOTH the <c>admin</c> and <c>user</c> realm roles and client scopes plus an admin test
///   user and the API client;</item>
///   <item>a <b>policy assertion</b> that a principal carrying the <c>admin</c> role (the role-claim
///   shape the realm's role-mapper emits) satisfies the shared <see cref="AdminAuthorization"/>
///   admin-only policy used by Steps 10/14, while a <c>user</c>-only principal does NOT and an
///   anonymous principal does NOT.</item>
/// </list>
/// </summary>
public sealed class KeycloakRealmAndAdminPolicyTests
{
    private static JsonElement LoadRealm()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "keycloak", "realm-export.json");
        File.Exists(path).Should().BeTrue($"the dev realm export should be copied to the test output at {path}");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.Clone();
    }

    [Fact]
    public void Realm_export_defines_admin_and_user_roles()
    {
        var realm = LoadRealm();

        var roleNames = realm.GetProperty("roles").GetProperty("realm")
            .EnumerateArray()
            .Select(r => r.GetProperty("name").GetString())
            .ToArray();

        roleNames.Should().Contain("admin");
        roleNames.Should().Contain("user");
    }

    [Fact]
    public void Realm_export_defines_admin_and_user_client_scopes()
    {
        var realm = LoadRealm();

        var scopeNames = realm.GetProperty("clientScopes")
            .EnumerateArray()
            .Select(s => s.GetProperty("name").GetString())
            .ToArray();

        scopeNames.Should().Contain("admin");
        scopeNames.Should().Contain("user");
    }

    [Fact]
    public void Realm_export_defines_the_api_client_and_an_admin_test_user()
    {
        var realm = LoadRealm();

        realm.GetProperty("clients").EnumerateArray()
            .Select(c => c.GetProperty("clientId").GetString())
            .Should().Contain("wheretoeat-api");

        var adminUser = realm.GetProperty("users").EnumerateArray()
            .FirstOrDefault(u => u.GetProperty("realmRoles").EnumerateArray().Any(r => r.GetString() == "admin"));
        adminUser.ValueKind.Should().Be(JsonValueKind.Object, "the realm should ship an admin test user");
    }

    [Fact]
    public async Task Admin_role_token_satisfies_the_admin_only_policy()
    {
        var (authorization, mapper) = BuildAuthorization();
        var principal = PrincipalWithRoles("admin");

        var result = await authorization.AuthorizeAsync(principal, resource: null, AdminAuthorization.PolicyName);

        result.Succeeded.Should().BeTrue("an admin-role token must satisfy the admin-only policy (Steps 10/14)");
        mapper.Map(principal).Should().Contain(UserRole.Admin);
    }

    [Fact]
    public async Task User_role_token_is_denied_by_the_admin_only_policy()
    {
        var (authorization, _) = BuildAuthorization();
        var principal = PrincipalWithRoles("user");

        var result = await authorization.AuthorizeAsync(principal, resource: null, AdminAuthorization.PolicyName);

        result.Succeeded.Should().BeFalse("a user-only token must NOT satisfy the admin-only policy (would be 403)");
    }

    [Fact]
    public async Task Anonymous_principal_is_denied_by_the_admin_only_policy()
    {
        var (authorization, _) = BuildAuthorization();
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity()); // unauthenticated

        var result = await authorization.AuthorizeAsync(anonymous, resource: null, AdminAuthorization.PolicyName);

        result.Succeeded.Should().BeFalse("an anonymous request must fail authentication (would be 401)");
    }

    private static (IAuthorizationService Authorization, IClaimsToRoleMapper Mapper) BuildAuthorization()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IClaimsToRoleMapper, RoleClaimMapper>();
        services.AddAdminAuthorizationHandler();
        services.AddAuthorization(AdminAuthorization.AddAdminPolicy);

        var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<IAuthorizationService>(), provider.GetRequiredService<IClaimsToRoleMapper>());
    }

    private static ClaimsPrincipal PrincipalWithRoles(params string[] roles)
    {
        // "role" is the claim type the realm's realm-role mapper emits and RoleClaimMapper reads.
        var claims = roles.Select(r => new Claim("role", r)).ToList();
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"));
    }
}
