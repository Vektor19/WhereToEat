using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace WhereToEat.PublicApi.Integration;

/// <summary>
/// Mints HS256-signed JWTs with the fixture's <see cref="PublicApiFixture.TestSigningKey"/> so the
/// integration tests exercise the auth seam (a valid token → 200, an invalid/absent token → 401)
/// without standing up a live IdP. This is a test-only signer; the host accepts it only because the
/// test injects the matching symmetric key via the environment.
/// </summary>
internal static class TestJwt
{
    /// <summary>A well-formed HS256 JWT signed with the fixture's trusted key — the host accepts it.</summary>
    public static string Valid(string subject = "test-subject", params string[] roles)
        => Signed(PublicApiFixture.TestSigningKey, subject, roles);

    /// <summary>
    /// A structurally valid, well-formed HS256 JWT signed with a DIFFERENT key the host does NOT
    /// trust. The token parses fine, so a 401 here proves the host actually verifies the signature
    /// (not merely that the string is malformed). Must be ≥ 32 bytes for HS256.
    /// </summary>
    public static string SignedWithUntrustedKey(string subject = "test-subject", params string[] roles)
        => Signed("a-completely-different-key-the-host-does-not-trust-0123456789", subject, roles);

    private static string Signed(string signingKey, string subject, IEnumerable<string> roles)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim> { new("sub", subject) };
        claims.AddRange(roles.Select(r => new Claim("role", r)));

        var token = new JwtSecurityToken(
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
