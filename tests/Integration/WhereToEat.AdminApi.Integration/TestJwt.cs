using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace WhereToEat.AdminApi.Integration;

/// <summary>
/// Mints HS256-signed JWTs with the fixture's <see cref="AdminApiFixture.TestSigningKey"/> so the admin
/// authorization behaviour is exercisable without a live IdP: an <c>admin</c>-role token satisfies the
/// admin-only policy (200), a <c>user</c>-role token does not (403), and no token is rejected by
/// authentication (401). The role claim shape matches what the <c>RoleClaimMapper</c> reads, which is
/// the same shape the Step 13 dev Keycloak realm emits.
/// </summary>
internal static class TestJwt
{
    /// <summary>A token carrying the <c>admin</c> role — satisfies the admin-only policy.</summary>
    public static string Admin(string subject = "admin-1") => Signed(subject, "admin");

    /// <summary>A token carrying only the <c>user</c> role — authenticated but NOT an admin (403).</summary>
    public static string User(string subject = "user-1") => Signed(subject, "user");

    private static string Signed(string subject, params string[] roles)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(AdminApiFixture.TestSigningKey));
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
