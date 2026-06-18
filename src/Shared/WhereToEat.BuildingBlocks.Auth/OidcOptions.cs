namespace WhereToEat.BuildingBlocks.Auth;

/// <summary>
/// Configuration for the OIDC resource-server bearer validation (bound from the host's
/// <c>Authentication:Oidc</c> section). We validate tokens issued by the EXTERNAL IdP at
/// <see cref="Authority"/>; we never issue tokens ourselves.
/// </summary>
public sealed class OidcOptions
{
    /// <summary>The configuration section these options bind from.</summary>
    public const string SectionName = "Authentication:Oidc";

    /// <summary>The IdP authority (issuer) base URL — its discovery/JWKS endpoints are derived from it.</summary>
    public string Authority { get; set; } = string.Empty;

    /// <summary>The expected audience of accepted tokens. When blank, audience validation is skipped.</summary>
    public string? Audience { get; set; }

    /// <summary>
    /// When <c>true</c>, the handler requires HTTPS metadata. Defaults to <c>true</c>; a local dev
    /// IdP over plain HTTP sets this to <c>false</c> in Development config only.
    /// </summary>
    public bool RequireHttpsMetadata { get; set; } = true;
}
