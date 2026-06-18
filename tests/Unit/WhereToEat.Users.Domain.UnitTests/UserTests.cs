using FluentAssertions;
using WhereToEat.Users.Domain;
using Xunit;

namespace WhereToEat.Users.Domain.UnitTests;

/// <summary>
/// The minimal, PII-free user model (invariant #11): identity is the opaque external IdP subject, the
/// baseline User role is always present, and Admin is grantable/revocable.
/// </summary>
public sealed class UserTests
{
    private static ExternalSubject Subject() =>
        ExternalSubject.Create("https://idp.example/realms/wte", "sub-123").Value;

    [Fact]
    public void ExternalSubject_RejectsBlankIssuerOrSubject()
    {
        ExternalSubject.Create("  ", "sub").IsFailure.Should().BeTrue();
        ExternalSubject.Create("iss", "  ").IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ExternalSubject_EqualityIsByValue()
    {
        var a = ExternalSubject.Create("iss", "sub").Value;
        var b = ExternalSubject.Create("iss", "sub").Value;

        a.Should().Be(b);
    }

    [Fact]
    public void Register_AlwaysGrantsTheBaselineUserRole()
    {
        var user = User.Register(Subject()).Value;

        user.IsInRole(UserRole.User).Should().BeTrue();
        user.IsInRole(UserRole.Admin).Should().BeFalse();
    }

    [Fact]
    public void Register_RejectsNullExternalSubject()
    {
        var result = User.Register(null!);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("User.ExternalSubjectRequired");
    }

    [Fact]
    public void Register_WithAdminRole_HoldsBothRoles()
    {
        var user = User.Register(Subject(), UserRole.Admin).Value;

        user.IsInRole(UserRole.Admin).Should().BeTrue();
        user.IsInRole(UserRole.User).Should().BeTrue("every user is at minimum a regular User");
    }

    [Fact]
    public void GrantRole_IsIdempotent()
    {
        var user = User.Register(Subject()).Value;

        user.GrantRole(UserRole.Admin);
        user.GrantRole(UserRole.Admin);

        user.Roles.Should().Contain(UserRole.Admin);
        user.Roles.Count(r => r == UserRole.Admin).Should().Be(1);
    }

    [Fact]
    public void RevokeRole_RemovesAdmin()
    {
        var user = User.Register(Subject(), UserRole.Admin).Value;

        user.RevokeRole(UserRole.Admin).IsSuccess.Should().BeTrue();

        user.IsInRole(UserRole.Admin).Should().BeFalse();
    }

    [Fact]
    public void RevokeRole_CannotRemoveBaselineUserRole()
    {
        var user = User.Register(Subject()).Value;

        var result = user.RevokeRole(UserRole.User);

        result.IsFailure.Should().BeTrue();
        user.IsInRole(UserRole.User).Should().BeTrue();
    }
}
