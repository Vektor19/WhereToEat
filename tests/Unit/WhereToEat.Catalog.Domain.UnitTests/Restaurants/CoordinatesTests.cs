using System.Linq;
using System.Reflection;
using FluentAssertions;
using WhereToEat.Catalog.Domain.Restaurants;
using WhereToEat.SharedKernel.ValueObjects;
using Xunit;

namespace WhereToEat.Catalog.Domain.UnitTests.Restaurants;

/// <summary>
/// Coordinates "no Google geo" guard (CLAUDE.md #7): the only spatial factory accepts an
/// OSM/Nominatim-sourced <see cref="GeoPoint"/>; no "from Google coordinates" path exists. Place ID
/// is an allowed optional field.
/// </summary>
public sealed class CoordinatesTests
{
    private static GeoPoint KyivPoint => GeoPoint.Create(50.4501, 30.5234).Value;

    [Fact]
    public void FromOsmGeoPoint_StoresPointAndOptionalGoogleIdentifiers()
    {
        var result = Coordinates.FromOsmGeoPoint(KyivPoint, placeId: "ChIJ123", mapsDeepLink: "https://maps/?q=1");

        result.IsSuccess.Should().BeTrue();
        result.Value.Point.Should().Be(KyivPoint);
        result.Value.PlaceId.Should().Be("ChIJ123");
        result.Value.MapsDeepLink.Should().Be("https://maps/?q=1");
    }

    [Fact]
    public void FromOsmGeoPoint_WithoutGoogleIdentifiers_LeavesThemNull()
    {
        var result = Coordinates.FromOsmGeoPoint(KyivPoint);

        result.IsSuccess.Should().BeTrue();
        result.Value.PlaceId.Should().BeNull();
        result.Value.MapsDeepLink.Should().BeNull();
    }

    [Fact]
    public void FromOsmGeoPoint_BlankGoogleIdentifiers_NormalizeToNull()
    {
        var result = Coordinates.FromOsmGeoPoint(KyivPoint, placeId: "   ", mapsDeepLink: "");

        result.Value.PlaceId.Should().BeNull();
        result.Value.MapsDeepLink.Should().BeNull();
    }

    [Fact]
    public void Coordinates_HasOnlyTheOsmGeoPointFactory()
    {
        // Invariant #7 encoded structurally: the only public Result-returning factory is the OSM one,
        // which takes an OSM/Nominatim <see cref="GeoPoint"/> (never raw lat/lng).
        var publicFactories = typeof(Coordinates)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.ReturnType.IsGenericType
                && m.ReturnType.GetGenericTypeDefinition().Name.StartsWith("Result", System.StringComparison.Ordinal))
            .ToList();

        publicFactories.Should().ContainSingle()
            .Which.Name.Should().Be(nameof(Coordinates.FromOsmGeoPoint));
    }

    [Fact]
    public void Coordinates_HasNoPublicMethodTakingTwoDoubles()
    {
        // The real "store raw Google lat/lng" attack vector is any public entry point — static OR
        // instance, regardless of name or return type — that accepts two doubles (lat, lng). Asserting
        // its total absence so it can't be slipped in later under a different name/signature (#7). The
        // OSM factory is safe: it takes a GeoPoint, not two doubles, so it does not trip this guard.
        var twoDoubleMethods = typeof(Coordinates)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
            .Where(m =>
            {
                var doubleParams = m.GetParameters().Count(p => p.ParameterType == typeof(double));
                return doubleParams >= 2;
            })
            .Select(m => m.Name)
            .ToList();

        twoDoubleMethods.Should().BeEmpty(
            "no public method may accept raw lat/lng doubles; coordinates come only from an OSM GeoPoint (invariant #7)");
    }
}
