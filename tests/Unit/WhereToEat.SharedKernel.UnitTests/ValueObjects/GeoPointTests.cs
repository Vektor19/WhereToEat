using FluentAssertions;
using WhereToEat.SharedKernel.ValueObjects;
using Xunit;

namespace WhereToEat.SharedKernel.UnitTests.ValueObjects;

public sealed class GeoPointTests
{
    [Theory]
    [InlineData(0d, 0d)]
    [InlineData(50.4501d, 30.5234d)]   // Kyiv
    [InlineData(-90d, -180d)]          // lower bounds inclusive
    [InlineData(90d, 180d)]            // upper bounds inclusive
    public void Create_WithinBounds_Succeeds(double lat, double lon)
    {
        var result = GeoPoint.Create(lat, lon);

        result.IsSuccess.Should().BeTrue();
        result.Value.Latitude.Should().Be(lat);
        result.Value.Longitude.Should().Be(lon);
    }

    [Theory]
    [InlineData(90.0001d)]
    [InlineData(-90.0001d)]
    [InlineData(1000d)]
    public void Create_LatitudeOutOfRange_Fails(double lat)
    {
        var result = GeoPoint.Create(lat, 0d);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("GeoPoint.LatitudeOutOfRange");
    }

    [Theory]
    [InlineData(180.0001d)]
    [InlineData(-180.0001d)]
    [InlineData(5000d)]
    public void Create_LongitudeOutOfRange_Fails(double lon)
    {
        var result = GeoPoint.Create(0d, lon);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("GeoPoint.LongitudeOutOfRange");
    }

    [Theory]
    [InlineData(double.NaN, 0d)]
    [InlineData(0d, double.NaN)]
    [InlineData(double.PositiveInfinity, 0d)]
    [InlineData(0d, double.NegativeInfinity)]
    public void Create_NonFiniteCoordinate_Fails(double lat, double lon)
    {
        var result = GeoPoint.Create(lat, lon);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Equality_IsByValue()
    {
        var a = GeoPoint.Create(50.45d, 30.52d).Value;
        var b = GeoPoint.Create(50.45d, 30.52d).Value;
        var c = GeoPoint.Create(49.84d, 24.03d).Value;

        a.Should().Be(b);
        (a == b).Should().BeTrue();
        a.Should().NotBe(c);
    }
}
