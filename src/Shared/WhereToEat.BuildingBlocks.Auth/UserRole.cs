namespace WhereToEat.BuildingBlocks.Auth;

/// <summary>
/// The application's own roles, mapped from the external IdP's claims by the
/// <see cref="IClaimsToRoleMapper"/>. Keeping our own enum (rather than leaking raw IdP claim
/// strings into the modules) is the swappable-identity seam: only the mapper knows the IdP's claim
/// shape, so changing IdP never ripples past this building block.
/// </summary>
public enum UserRole
{
    /// <summary>A regular authenticated end user (can rate, save selections, …).</summary>
    User = 0,

    /// <summary>An administrator (admin host CRUD, protection flags, monetization — later steps).</summary>
    Admin = 1,
}
