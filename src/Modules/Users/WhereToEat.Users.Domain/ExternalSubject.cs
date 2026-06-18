using WhereToEat.SharedKernel.Primitives;
using WhereToEat.SharedKernel.Results;

namespace WhereToEat.Users.Domain;

/// <summary>
/// The user's identity at the <b>external OIDC identity provider</b>: the issuer (<c>iss</c>) plus the
/// stable subject (<c>sub</c>) claim. This is the only identity link we store — there is deliberately
/// <b>no email, name, or other PII</b> on the <see cref="User"/> aggregate (invariant #11: PII
/// minimized, never venue-exposed). Carrying the issuer alongside the subject keeps the seam correct
/// if the IdP is swapped or a second issuer is added (subjects are only unique within an issuer).
///
/// Equality is by value (issuer + subject), so the same external identity maps to the same value.
/// </summary>
public sealed class ExternalSubject : ValueObject
{
    private ExternalSubject(string issuer, string subject)
    {
        Issuer = issuer;
        Subject = subject;
    }

    /// <summary>The OIDC issuer (the <c>iss</c> claim / IdP authority).</summary>
    public string Issuer { get; }

    /// <summary>The stable per-issuer subject id (the <c>sub</c> claim) — opaque, no PII.</summary>
    public string Subject { get; }

    /// <summary>
    /// Creates the external-subject reference, rejecting a blank issuer or subject. The values are
    /// trimmed; they are treated as opaque identifiers, not parsed.
    /// </summary>
    public static Result<ExternalSubject> Create(string issuer, string subject)
    {
        if (string.IsNullOrWhiteSpace(issuer))
        {
            return Result.Failure<ExternalSubject>(
                Error.Validation("ExternalSubject.IssuerRequired", "An external subject must carry the OIDC issuer."));
        }

        if (string.IsNullOrWhiteSpace(subject))
        {
            return Result.Failure<ExternalSubject>(
                Error.Validation("ExternalSubject.SubjectRequired", "An external subject must carry the OIDC subject id."));
        }

        return Result.Success(new ExternalSubject(issuer.Trim(), subject.Trim()));
    }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Issuer;
        yield return Subject;
    }
}
