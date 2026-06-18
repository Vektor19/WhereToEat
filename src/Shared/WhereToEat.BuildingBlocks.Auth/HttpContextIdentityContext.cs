using Microsoft.AspNetCore.Http;

namespace WhereToEat.BuildingBlocks.Auth;

/// <summary>
/// The ASP.NET-backed <see cref="IIdentityContext"/>: it reads the current request's validated
/// <c>HttpContext.User</c> (via <see cref="IHttpContextAccessor"/>) and projects it through the
/// <see cref="IClaimsToRoleMapper"/>. This is the only place that touches <c>HttpContext</c>, so the
/// rest of the code depends purely on the <see cref="IIdentityContext"/> abstraction.
/// </summary>
public sealed class HttpContextIdentityContext : IIdentityContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IClaimsToRoleMapper _roleMapper;

    public HttpContextIdentityContext(IHttpContextAccessor httpContextAccessor, IClaimsToRoleMapper roleMapper)
    {
        ArgumentNullException.ThrowIfNull(httpContextAccessor);
        ArgumentNullException.ThrowIfNull(roleMapper);
        _httpContextAccessor = httpContextAccessor;
        _roleMapper = roleMapper;
    }

    /// <inheritdoc />
    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;

    /// <inheritdoc />
    public string? SubjectId
    {
        get
        {
            if (!IsAuthenticated)
            {
                return null;
            }

            var principal = _httpContextAccessor.HttpContext!.User;
            // OIDC subject lands either as the standard "sub" claim or NameIdentifier depending on
            // the handler's claim mapping; check both so the seam is handler-agnostic.
            return principal.FindFirst("sub")?.Value
                ?? principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        }
    }

    /// <inheritdoc />
    public IReadOnlyCollection<UserRole> Roles
    {
        get
        {
            if (!IsAuthenticated)
            {
                return [];
            }

            return _roleMapper.Map(_httpContextAccessor.HttpContext!.User);
        }
    }

    /// <inheritdoc />
    public bool IsInRole(UserRole role) => Roles.Contains(role);
}
