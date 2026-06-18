using FluentAssertions;
using WhereToEat.Geo.Application;
using WhereToEat.SharedKernel.Geo;
using WhereToEat.SharedKernel.ValueObjects;
using Xunit;

namespace WhereToEat.Geo.UnitTests;

/// <summary>
/// Confirms the Geo module's distance facade reuses the SharedKernel Haversine math (invariant #7 —
/// local distance, no paid API) rather than re-implementing it.
/// </summary>
public sealed class GeoDistanceTests
{
    [Fact]
    public void KilometresBetween_MatchesSharedKernelHaversine()
    {
        var kyiv = GeoPoint.Create(50.4501, 30.5234).Value;
        var lviv = GeoPoint.Create(49.8397, 24.0297).Value;

        GeoDistance.KilometresBetween(kyiv, lviv)
            .Should().Be(Haversine.DistanceKm(kyiv, lviv));
    }

    [Fact]
    public void KilometresBetween_IsZero_ForTheSamePoint()
    {
        var point = GeoPoint.Create(50.4501, 30.5234).Value;
        GeoDistance.KilometresBetween(point, point).Should().Be(0d);
    }
}
