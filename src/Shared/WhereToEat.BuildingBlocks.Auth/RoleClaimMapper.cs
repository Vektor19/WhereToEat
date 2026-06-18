using System.Security.Claims;

namespace WhereToEat.BuildingBlocks.Auth;

/// <summary>
/// The default <see cref="IClaimsToRoleMapper"/>: reads role strings from the common role-claim
/// types (<see cref="ClaimTypes.Role"/>, the OIDC/JWT <c>role</c> and <c>roles</c> claims) and maps
/// the well-known strings <c>admin</c>/<c>user</c> (case-insensitive) onto our
/// <see cref="UserRole"/> enum. Unknown role strings are ignored rather than failing — an IdP often
/// carries roles we do not model. This default fits a Keycloak-style realm (Step 13 defines the
/// <c>admin</c>/<c>user</c> realm roles); a host can register a different mapper to fit another IdP.
/// </summary>
public sealed class RoleClaimMapper : IClaimsToRoleMapper
{
    private static readonly string[] RoleClaimTypes = [ClaimTypes.Role, "role", "roles"];

    /// <inheritdoc />
    public IReadOnlyCollection<UserRole> Map(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var roles = new HashSet<UserRole>();

        foreach (var claim in principal.Claims)
        {
            if (!RoleClaimTypes.Contains(claim.Type, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            if (TryMap(claim.Value, out var role))
            {
                roles.Add(role);
            }
        }

        return roles;
    }

    private static bool TryMap(string value, out UserRole role)
    {
        if (string.Equals(value, "admin", StringComparison.OrdinalIgnoreCase))
        {
            role = UserRole.Admin;
            return true;
        }

        if (string.Equals(value, "user", StringComparison.OrdinalIgnoreCase))
        {
            role = UserRole.User;
            return true;
        }

        role = default;
        return false;
    }
}
