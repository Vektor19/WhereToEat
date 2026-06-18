using FluentAssertions;
using WhereToEat.SharedKernel.Geo;
using WhereToEat.SharedKernel.ValueObjects;
using Xunit;

namespace WhereToEat.SharedKernel.UnitTests.Geo;

public sealed class HaversineTests
{
    // Known coordinate pairs and their great-circle distance in km. The expected values
    // are the accepted great-circle distances for these well-known city centres; the
    // assertion tolerance is the acceptance criterion's 0.5%.
    [Theory]
    [InlineData(50.4501, 30.5234, 49.8397, 24.0297, 467.531)]  // Kyiv ↔ Lviv
    [InlineData(50.4501, 30.5234, 46.4825, 30.7233, 441.423)]  // Kyiv ↔ Odesa
    [InlineData(50.4501, 30.5234, 49.9935, 36.2304, 409.080)]  // Kyiv ↔ Kharkiv
    [InlineData(51.5074, -0.1278, 48.8566, 2.3522, 343.557)]   // London ↔ Paris
    public void DistanceKm_ForKnownPairs_IsWithinHalfPercent(
        double lat1, double lon1, double lat2, double lon2, double expectedKm)
    {
        var a = GeoPoint.Create(lat1, lon1).Value;
        var b = GeoPoint.Create(lat2, lon2).Value;

        var distance = Haversine.DistanceKm(a, b);

        var tolerance = expectedKm * 0.005d;
        distance.Should().BeApproximately(expectedKm, tolerance);
    }

    [Fact]
    public void DistanceKm_FromPointToItself_IsExactlyZero()
    {
        var a = GeoPoint.Create(50.4501d, 30.5234d).Value;

        Haversine.DistanceKm(a, a).Should().Be(0d);
    }

    [Fact]
    public void DistanceKm_ToAnEqualButDistinctPoint_IsExactlyZero()
    {
        var a = GeoPoint.Create(50.4501d, 30.5234d).Value;
        var b = GeoPoint.Create(50.4501d, 30.5234d).Value;

        Haversine.DistanceKm(a, b).Should().Be(0d);
    }

    [Fact]
    public void DistanceKm_IsSymmetric()
    {
        var a = GeoPoint.Create(50.4501d, 30.5234d).Value;
        var b = GeoPoint.Create(49.8397d, 24.0297d).Value;

        Haversine.DistanceKm(a, b).Should().BeApproximately(Haversine.DistanceKm(b, a), 1e-9d);
    }

    [Fact]
    public void DistanceKm_NullArgument_Throws()
    {
        var a = GeoPoint.Create(0d, 0d).Value;

        var act = () => Haversine.DistanceKm(a, null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
