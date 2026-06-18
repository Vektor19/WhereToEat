using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace WhereToEat.BuildingBlocks.Auth;

/// <summary>
/// Wires the OIDC resource-server auth: JWT bearer validation against the external IdP authority/
/// JWKS, the identity seam (<see cref="IIdentityContext"/> + a claims→role mapper), and named
/// authorization policies for the application roles. The host calls this from its composition root.
/// Public read endpoints stay anonymous; the seam/policies are in place for the later authenticated
/// endpoints (ratings, admin). We validate tokens only — no token issuance lives here.
/// </summary>
public static class AuthServiceCollectionExtensions
{
    /// <summary>Authorization policy name requiring an authenticated <see cref="UserRole.User"/> (or admin).</summary>
    public const string UserPolicy = "RequireUser";

    /// <summary>
    /// Authorization policy name requiring an authenticated <see cref="UserRole.Admin"/>. Aliases the
    /// canonical <see cref="AdminAuthorization.PolicyName"/> so callers can reference either name and
    /// the admin host (Step 10) and the public host share one admin-only policy definition.
    /// </summary>
    public const string AdminPolicy = AdminAuthorization.PolicyName;

    /// <summary>
    /// Configuration sub-key (under <see cref="OidcOptions.SectionName"/>) for the Dev/Testing-only
    /// symmetric test signing key. Tests set it via per-host config (<c>UseSetting</c>); it is read
    /// only in Development/Testing and ignored everywhere else, so it can never bypass production auth.
    /// </summary>
    public const string TestSigningKeyConfigKey = "TestSigningKey";

    /// <summary>
    /// Environment names where the relaxed test seam (an injected symmetric signing key, no live
    /// IdP) is allowed. Anywhere else the host validates ONLY against the IdP's JWKS with issuer/
    /// audience/lifetime checks on, and a blank authority fails fast.
    /// </summary>
    private const string DevelopmentEnvironment = "Development";
    private const string TestingEnvironment = "Testing";

    /// <summary>
    /// Adds the JWT bearer authentication, the identity-context seam, and the role policies.
    /// <para>
    /// In production-like environments the bearer is validated ONLY against the configured external
    /// IdP's JWKS with issuer/audience/lifetime checks on; a blank <see cref="OidcOptions.Authority"/>
    /// fails fast at startup (it must never silently weaken issuer validation). The Dev/Testing-only
    /// test seam — accepting tokens signed with a configured symmetric key so the 401/200 behaviour
    /// is exercisable without a live IdP — is gated on <paramref name="environmentName"/> and is
    /// completely unreachable in any other environment, so a leaked test key cannot bypass auth.
    /// </para>
    /// <paramref name="environmentName"/> is the host's environment (e.g. <c>Development</c>,
    /// <c>Testing</c>, <c>Production</c>); the host passes <c>IWebHostEnvironment.EnvironmentName</c>.
    /// </summary>
    public static IServiceCollection AddOidcResourceServerAuth(
        this IServiceCollection services,
        IConfiguration configuration,
        string environmentName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentName);

        services.Configure<OidcOptions>(configuration.GetSection(OidcOptions.SectionName));

        var oidc = configuration.GetSection(OidcOptions.SectionName).Get<OidcOptions>() ?? new OidcOptions();

        var isDevOrTest =
            string.Equals(environmentName, DevelopmentEnvironment, StringComparison.OrdinalIgnoreCase)
            || string.Equals(environmentName, TestingEnvironment, StringComparison.OrdinalIgnoreCase);

        // Fail fast: outside Dev/Testing a blank authority would silently disable issuer validation
        // (no JWKS to validate against). A missing prod authority is a misconfiguration, not a
        // reason to weaken security — surface it at startup.
        if (!isDevOrTest && string.IsNullOrWhiteSpace(oidc.Authority))
        {
            throw new InvalidOperationException(
                $"'{OidcOptions.SectionName}:Authority' must be configured in the '{environmentName}' " +
                "environment — the resource server validates tokens against the IdP's JWKS and must " +
                "not run with issuer validation disabled. Set the external IdP authority URL.");
        }

        // The Dev/Testing-only symmetric test key (read from configuration, NOT from a process-wide
        // env var, so it stays scoped to the host that set it). Ignored entirely outside Dev/Testing.
        var testSigningKey = isDevOrTest
            ? configuration[$"{OidcOptions.SectionName}:{TestSigningKeyConfigKey}"]
            : null;

        services.AddHttpContextAccessor();
        services.TryAddSingleton<IClaimsToRoleMapper, RoleClaimMapper>();
        services.AddScoped<IIdentityContext, HttpContextIdentityContext>();

        // The admin-only policy's requirement is satisfied by a handler that resolves the registered
        // IClaimsToRoleMapper from DI — so a host that swaps the mapper has it honoured here too.
        services.AddAdminAuthorizationHandler();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Validate tokens from the configured external IdP. The authority drives metadata/
                // JWKS retrieval; we never sign tokens ourselves.
                options.Authority = string.IsNullOrWhiteSpace(oidc.Authority) ? null : oidc.Authority;
                options.RequireHttpsMetadata = oidc.RequireHttpsMetadata;
                options.TokenValidationParameters.ValidateIssuer = !string.IsNullOrWhiteSpace(oidc.Authority);

                if (string.IsNullOrWhiteSpace(oidc.Audience))
                {
                    options.TokenValidationParameters.ValidateAudience = false;
                }
                else
                {
                    options.Audience = oidc.Audience;
                }

                // Dev/Testing seam ONLY: accept tokens signed with the configured symmetric key so
                // the auth behaviour is testable without a live IdP. This branch cannot run outside
                // Dev/Testing (isDevOrTest gate above leaves testSigningKey null), so production
                // always validates against the IdP JWKS with issuer/audience/lifetime on.
                if (!string.IsNullOrWhiteSpace(testSigningKey))
                {
                    options.RequireHttpsMetadata = false;
                    options.TokenValidationParameters.ValidateIssuer = false;
                    options.TokenValidationParameters.ValidateAudience = false;
                    options.TokenValidationParameters.ValidateIssuerSigningKey = true;
                    options.TokenValidationParameters.IssuerSigningKey =
                        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(testSigningKey));
                }
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(UserPolicy, policy => policy.RequireAuthenticatedUser());

            // The admin-only policy lives in AdminAuthorization (one definition shared by the public
            // host and the Step 10 admin host); register it here so both hosts get the same policy.
            AdminAuthorization.AddAdminPolicy(options);
        });

        return services;
    }
}
