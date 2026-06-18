using FluentAssertions;
using WhereToEat.Monetization.Domain;
using WhereToEat.Monetization.Domain.Identifiers;
using Xunit;

namespace WhereToEat.Monetization.Domain.UnitTests;

/// <summary>
/// VerifiedStatus gates the real-photo permission (Step 5 photo gate) and carries the tier; it never
/// affects organic ranking (the labeled-ad invariant test pins the no-rank-field shape separately).
/// </summary>
public sealed class VerifiedStatusTests
{
    private static readonly VenueRef Venue = VenueRef.From(Guid.NewGuid());

    [Fact]
    public void Unverified_IsTheFreemiumBaseline_NoRealPhotos()
    {
        var status = VerifiedStatus.Unverified(Venue).Value;

        status.IsVerified.Should().BeFalse();
        status.Tier.Should().Be(SubscriptionTier.None);
        status.CanManageRealPhotos.Should().BeFalse();
    }

    [Theory]
    [InlineData(SubscriptionTier.Basic)]
    [InlineData(SubscriptionTier.Pro)]
    public void Grant_PaidTier_UnlocksRealPhotos(SubscriptionTier tier)
    {
        var status = VerifiedStatus.Unverified(Venue).Value;

        status.Grant(tier).IsSuccess.Should().BeTrue();

        status.IsVerified.Should().BeTrue();
        status.Tier.Should().Be(tier);
        status.CanManageRealPhotos.Should().BeTrue();
    }

    [Fact]
    public void Grant_None_IsRejected()
    {
        var status = VerifiedStatus.Unverified(Venue).Value;

        status.Grant(SubscriptionTier.None).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Revoke_ReturnsToBaseline()
    {
        var status = VerifiedStatus.Create(Venue, SubscriptionTier.Pro).Value;

        status.Revoke();

        status.IsVerified.Should().BeFalse();
        status.CanManageRealPhotos.Should().BeFalse();
    }

    [Fact]
    public void Create_RejectsDefaultVenue()
    {
        VerifiedStatus.Create(default, SubscriptionTier.Basic).IsFailure.Should().BeTrue();
    }
}
