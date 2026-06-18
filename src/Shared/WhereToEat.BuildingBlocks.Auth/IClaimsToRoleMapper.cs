using System.Security.Claims;

namespace WhereToEat.BuildingBlocks.Auth;

/// <summary>
/// Maps a validated token's <see cref="ClaimsPrincipal"/> to our own <see cref="UserRole"/> set.
/// This is the single place that knows the IdP's claim shape (which claim type carries roles, what
/// the role strings are) — swapping IdP means swapping only this mapper, not the modules. Kept as
/// an interface so a host (or a test) can supply its own mapping.
/// </summary>
public interface IClaimsToRoleMapper
{
    /// <summary>Returns the application roles encoded in <paramref name="principal"/>'s claims.</summary>
    IReadOnlyCollection<UserRole> Map(ClaimsPrincipal principal);
}
