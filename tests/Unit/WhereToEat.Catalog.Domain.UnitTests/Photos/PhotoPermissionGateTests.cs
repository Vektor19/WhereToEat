using FluentAssertions;
using WhereToEat.Catalog.Domain.Identifiers;
using WhereToEat.Catalog.Domain.Photos;
using Xunit;

namespace WhereToEat.Catalog.Domain.UnitTests.Photos;

/// <summary>
/// The invariant-#8 photo gate: the generic category photo is our own content and the displayable
/// default on every dish, while a real venue photo is displayable only when permission is granted.
/// </summary>
public sealed class PhotoPermissionGateTests
{
    private static readonly DishId Dish = DishId.New();

    [Fact]
    public void Generic_IsAlwaysDisplayable_AndIsTheDefault()
    {
        var photo = Photo.Generic(Dish, "https://cdn.wte/generic/pizza.png").Value;

        photo.IsGeneric.Should().BeTrue();
        photo.CanBeDisplayed().Should().BeTrue();
    }

    [Fact]
    public void RealWithPermission_IsDisplayable()
    {
        var photo = Photo.RealWithPermission(Dish, "https://venue.example/real.jpg").Value;

        photo.IsGeneric.Should().BeFalse();
        photo.PermissionGranted.Should().BeTrue();
        photo.CanBeDisplayed().Should().BeTrue();
    }

    [Fact]
    public void RealPhoto_BecomesNonDisplayable_WhenPermissionRevoked()
    {
        var photo = Photo.RealWithPermission(Dish, "https://venue.example/real.jpg").Value;

        photo.RevokePermission();

        photo.PermissionGranted.Should().BeFalse();
        photo.CanBeDisplayed().Should().BeFalse("a real photo without permission may never be shown (invariant #8)");
    }

    [Fact]
    public void Generic_RevokePermission_IsANoOp_StillDisplayable()
    {
        var photo = Photo.Generic(Dish, "https://cdn.wte/generic/pizza.png").Value;

        photo.RevokePermission();

        photo.CanBeDisplayed().Should().BeTrue("a generic photo is our own content and always displayable");
    }

    [Fact]
    public void Photo_RejectsBlankUrl()
    {
        Photo.Generic(Dish, "  ").IsFailure.Should().BeTrue();
        Photo.RealWithPermission(Dish, "  ").IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Rehydrate_ReconstructsRealButRevokedState_AndStaysNonDisplayable()
    {
        // The state RevokePermission() produces (real, no permission) — which the schema persists but
        // neither creation factory can express — must round-trip back through Rehydrate intact.
        var id = PhotoId.New();

        var photo = Photo.Rehydrate(id, Dish, isGeneric: false, permissionGranted: false, "https://venue.example/real.jpg").Value;

        photo.Id.Should().Be(id);
        photo.IsGeneric.Should().BeFalse();
        photo.PermissionGranted.Should().BeFalse();
        photo.CanBeDisplayed().Should().BeFalse("a real photo without permission may never be shown (invariant #8)");
    }

    [Fact]
    public void Rehydrate_RejectsGenericWithGrantedPermission()
    {
        // Mirrors the CK_Photo_GenericHasNoPermission guard: our own content never carries a permission.
        Photo.Rehydrate(PhotoId.New(), Dish, isGeneric: true, permissionGranted: true, "https://cdn.wte/generic/pizza.png")
            .IsFailure.Should().BeTrue();
    }
}
