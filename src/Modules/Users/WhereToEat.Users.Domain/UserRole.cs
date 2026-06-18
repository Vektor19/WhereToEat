namespace WhereToEat.Users.Domain;

/// <summary>
/// The application role a user holds. Deliberately a small, closed enum — roles are mapped from the
/// IdP's claims/scopes by the auth seam (Step 6/10) and authorize endpoints; the <c>admin</c> role is
/// what the admin host's admin-only policy (Steps 10/14) checks. Kept here so the role mapping is a
/// domain concept the Users module owns, not a host-only string.
/// </summary>
public enum UserRole
{
    /// <summary>A regular end user: search, rate, save lists. The default for any authenticated user.</summary>
    User = 0,

    /// <summary>An operator with access to the admin host (manual data curation, protection flags, photos).</summary>
    Admin = 1,
}
