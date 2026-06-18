using WhereToEat.SharedKernel.Primitives;
using WhereToEat.SharedKernel.Results;
using WhereToEat.Users.Domain.Identifiers;

namespace WhereToEat.Users.Domain;

/// <summary>
/// A registered user — kept deliberately <b>minimal and PII-free</b> (invariant #11). The aggregate
/// carries only our internal <see cref="UserId"/>, the opaque <see cref="ExternalSubject"/> linking it
/// to the external IdP identity, and the set of <see cref="UserRole"/>s mapped from the IdP's claims.
/// There is <b>no email/name/phone</b> here: identity and contactable PII live at the IdP, never in
/// our store, and nothing about a user is ever exposed to venues (only aggregates are — §8.1).
///
/// <para>
/// Every user is at least a <see cref="UserRole.User"/>; the <see cref="UserRole.Admin"/> role is what
/// the admin host's admin-only policy (Steps 10/14) authorizes against.
/// </para>
/// </summary>
public sealed class User : AggregateRoot<UserId>
{
    private readonly HashSet<UserRole> _roles;

    private User(UserId id, ExternalSubject externalSubject, IEnumerable<UserRole> roles)
        : base(id)
    {
        ExternalSubject = externalSubject;
        _roles = [.. roles];

        // Every user is at minimum a regular User — the baseline role is never absent.
        _roles.Add(UserRole.User);
    }

    /// <summary>The opaque link to the external IdP identity (issuer + subject; no PII).</summary>
    public ExternalSubject ExternalSubject { get; }

    /// <summary>The roles this user holds (always includes <see cref="UserRole.User"/>).</summary>
    public IReadOnlyCollection<UserRole> Roles => _roles;

    /// <summary>Registers a new user with a fresh id for the given external subject and roles.</summary>
    public static Result<User> Register(ExternalSubject externalSubject, params UserRole[] roles)
        => Register(UserId.New(), externalSubject, roles);

    /// <summary>
    /// Registers a user (with an explicit id for rehydration/seeding) for the given external subject,
    /// rejecting a null subject. The baseline <see cref="UserRole.User"/> role is always added.
    /// </summary>
    public static Result<User> Register(UserId id, ExternalSubject externalSubject, params UserRole[] roles)
    {
        if (externalSubject is null)
        {
            return Result.Failure<User>(
                Error.Validation("User.ExternalSubjectRequired", "A user must be linked to an external IdP subject."));
        }

        return Result.Success(new User(id, externalSubject, roles ?? []));
    }

    /// <summary>True when the user holds the given role.</summary>
    public bool IsInRole(UserRole role) => _roles.Contains(role);

    /// <summary>Grants a role (idempotent — granting a held role is a no-op).</summary>
    public void GrantRole(UserRole role) => _roles.Add(role);

    /// <summary>
    /// Revokes a role. The baseline <see cref="UserRole.User"/> role cannot be revoked (every user is
    /// at least a regular user); revoking it returns a failure rather than silently no-op'ing.
    /// </summary>
    public Result RevokeRole(UserRole role)
    {
        if (role == UserRole.User)
        {
            return Result.Failure(
                Error.Validation("User.CannotRevokeBaselineRole", "The baseline User role cannot be revoked."));
        }

        _roles.Remove(role);
        return Result.Success();
    }
}
