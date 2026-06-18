using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace WhereToEat.BuildingBlocks.Auth;

/// <summary>
/// The <b>admin-only</b> authorization policy (Step 10). The admin API host validates the SAME OIDC
/// tokens as the public host but authorizes ONLY admin roles/scopes — an admin token → 200, a
/// non-admin authenticated user → 403, an anonymous request → 401. The policy is defined here, in one
/// place, so Steps 10 and 14 share the exact same definition and Step 13 can exercise it against the
/// dev Keycloak realm (whose export defines the <c>admin</c>/<c>user</c> roles). The membership test
/// is an <see cref="AdminRoleRequirement"/> whose <see cref="AdminRoleHandler"/> resolves the
/// registered <see cref="IClaimsToRoleMapper"/> from DI, so a host that swaps the mapper (a different
/// IdP claim shape) has that swap honoured by the admin policy too — the seam never stops at
/// <see cref="IIdentityContext"/>.
/// </summary>
public static class AdminAuthorization
{
    /// <summary>The admin-only policy name (an authenticated caller mapped to <see cref="UserRole.Admin"/>).</summary>
    public const string PolicyName = "RequireAdmin";

    /// <summary>
    /// Configures the admin-only policy on <paramref name="options"/>: the caller must be
    /// authenticated AND satisfy the <see cref="AdminRoleRequirement"/> (its handler maps the caller's
    /// claims via the registered <see cref="IClaimsToRoleMapper"/> and checks for
    /// <see cref="UserRole.Admin"/>). An unauthenticated caller fails authentication first (401); an
    /// authenticated non-admin fails the requirement (403).
    /// </summary>
    public static void AddAdminPolicy(AuthorizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.AddPolicy(PolicyName, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.AddRequirements(new AdminRoleRequirement());
        });
    }

    /// <summary>
    /// Registers the <see cref="AdminRoleHandler"/> so the <see cref="AdminRoleRequirement"/> can be
    /// satisfied. Idempotent (<c>TryAddEnumerable</c>): registering the handler twice is harmless when
    /// both the public host and the admin host wire the same auth building block.
    /// </summary>
    public static IServiceCollection AddAdminAuthorizationHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IAuthorizationHandler, AdminRoleHandler>();
        return services;
    }
}

/// <summary>
/// The authorization requirement satisfied when the caller's mapped roles include
/// <see cref="UserRole.Admin"/>. Carries no state — the mapping logic lives in
/// <see cref="AdminRoleHandler"/> so it can resolve the swappable <see cref="IClaimsToRoleMapper"/>
/// from DI.
/// </summary>
public sealed class AdminRoleRequirement : IAuthorizationRequirement;

/// <summary>
/// Resolves the registered <see cref="IClaimsToRoleMapper"/> and succeeds the
/// <see cref="AdminRoleRequirement"/> when the caller's mapped roles include
/// <see cref="UserRole.Admin"/>. Because the mapper comes from DI, a host that registers a custom
/// mapper (a different IdP's claim shape) has that mapping honoured by the admin policy.
/// </summary>
public sealed class AdminRoleHandler : AuthorizationHandler<AdminRoleRequirement>
{
    private readonly IClaimsToRoleMapper _mapper;

    public AdminRoleHandler(IClaimsToRoleMapper mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        _mapper = mapper;
    }

    /// <inheritdoc />
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AdminRoleRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requirement);

        if (_mapper.Map(context.User).Contains(UserRole.Admin))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
