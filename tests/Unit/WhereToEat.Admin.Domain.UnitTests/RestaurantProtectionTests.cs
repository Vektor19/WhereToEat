using FluentAssertions;
using WhereToEat.Admin.Domain.Protection;
using Xunit;

namespace WhereToEat.Admin.Domain.UnitTests;

/// <summary>
/// The restaurant-level admin > parser protection (invariant #3): DoNotUpdate makes the parser skip
/// the whole venue, complementing the per-MenuItem DoNotParse from Step 3.
/// </summary>
public sealed class RestaurantProtectionTests
{
    private static readonly VenueRef Venue = VenueRef.From(Guid.NewGuid());

    [Fact]
    public void Unprotected_AllowsParserUpdates()
    {
        var protection = RestaurantProtection.Unprotected(Venue).Value;

        protection.DoNotUpdate.Should().BeFalse();
        protection.ShouldParserSkip().Should().BeFalse();
    }

    [Fact]
    public void ProtectFromParser_MakesTheParserSkipTheVenue()
    {
        var protection = RestaurantProtection.Unprotected(Venue).Value;

        protection.ProtectFromParser();

        protection.DoNotUpdate.Should().BeTrue();
        protection.ShouldParserSkip().Should().BeTrue();
    }

    [Fact]
    public void AllowParserUpdates_ClearsTheFlag()
    {
        var protection = RestaurantProtection.Create(Venue, doNotUpdate: true).Value;

        protection.AllowParserUpdates();

        protection.DoNotUpdate.Should().BeFalse();
    }

    [Fact]
    public void Create_RejectsDefaultVenue()
    {
        RestaurantProtection.Create(default, doNotUpdate: true).IsFailure.Should().BeTrue();
    }
}
