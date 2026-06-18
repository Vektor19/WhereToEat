namespace WhereToEat.BuildingBlocks.Auth;

/// <summary>
/// The per-request identity seam the application/host code reads instead of touching ASP.NET's
/// <c>HttpContext.User</c> or raw claims directly. It exposes only what the domain cares about — the
/// external IdP subject id and the mapped <see cref="UserRole"/>s — so the IdP stays swappable: the
/// concrete <see cref="IClaimsToRoleMapper"/> owns the claim shape, everything else depends on this
/// abstraction. For an anonymous request <see cref="IsAuthenticated"/> is <c>false</c> and
/// <see cref="SubjectId"/> is <c>null</c>.
/// </summary>
public interface IIdentityContext
{
    /// <summary>True when the current request carries a validated token.</summary>
    bool IsAuthenticated { get; }

    /// <summary>The external IdP subject (<c>sub</c>) identifier, or <c>null</c> when anonymous.</summary>
    string? SubjectId { get; }

    /// <summary>The roles mapped from the token's claims; empty when anonymous.</summary>
    IReadOnlyCollection<UserRole> Roles { get; }

    /// <summary>True when the caller holds <paramref name="role"/>.</summary>
    bool IsInRole(UserRole role);
}
