using FluentAssertions;
using WhereToEat.Recommendation.Infrastructure.Medians;
using Xunit;

namespace WhereToEat.Worker.UnitTests;

/// <summary>
/// Unit tests for the pure <see cref="PriceMedianCalculator"/> (the nightly median job's core math):
/// the per-area vs. city-wide fallback selection below threshold N, the median computation, and the
/// dish/category two-level shape. These pin the behaviour the Step 12 integration test then asserts
/// the DB-only provider reads back.
/// </summary>
public sealed class PriceMedianCalculatorTests
{
    private static readonly Guid DishA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CategoryA = Guid.Parse("22222222-2222-2222-2222-222222222222");

    // Two coordinates in the same ~0.05° grid cell (so they share an AreaKey) and one far away.
    private const double LatCellA = 50.451;
    private const double LngCellA = 30.523;

    [Fact]
    public void Empty_input_produces_no_rows()
    {
        var rows = PriceMedianCalculator.Calculate(Array.Empty<PricedMenuItem>());

        rows.Should().BeEmpty();
    }

    [Fact]
    public void Always_emits_a_city_wide_row_per_selection()
    {
        var items = new[]
        {
            Item(DishA, CategoryA, 100m, LatCellA, LngCellA),
            Item(DishA, CategoryA, 200m, LatCellA, LngCellA),
        };

        var rows = PriceMedianCalculator.Calculate(items, areaSampleThreshold: 5);

        // Even though the single area is below N (2 < 5), the city-wide ('*') rows are always present
        // for both the dish and its category — that is the fallback the provider reads.
        rows.Should().Contain(r => r.AreaKey == "*" && r.DishId == DishA && r.CategoryId == null);
        rows.Should().Contain(r => r.AreaKey == "*" && r.CategoryId == CategoryA && r.DishId == null);
    }

    [Fact]
    public void Below_threshold_area_gets_no_per_area_row_only_city_wide()
    {
        var items = new[]
        {
            Item(DishA, CategoryA, 100m, LatCellA, LngCellA),
            Item(DishA, CategoryA, 120m, LatCellA, LngCellA),
        };

        var rows = PriceMedianCalculator.Calculate(items, areaSampleThreshold: 5);

        // 2 samples < N (5): no per-area dish row exists; only the city-wide one.
        rows.Should().NotContain(r => r.DishId == DishA && r.AreaKey != "*");
        rows.Should().ContainSingle(r => r.DishId == DishA && r.AreaKey == "*")
            .Which.MedianAmount.Should().Be(110m);
    }

    [Fact]
    public void At_or_above_threshold_area_gets_a_per_area_row()
    {
        // 5 samples in one area at threshold N = 5 → a per-area dish row appears.
        var items = new[]
        {
            Item(DishA, CategoryA, 100m, LatCellA, LngCellA),
            Item(DishA, CategoryA, 110m, LatCellA, LngCellA),
            Item(DishA, CategoryA, 120m, LatCellA, LngCellA),
            Item(DishA, CategoryA, 130m, LatCellA, LngCellA),
            Item(DishA, CategoryA, 140m, LatCellA, LngCellA),
        };

        var rows = PriceMedianCalculator.Calculate(items, areaSampleThreshold: 5);

        var perArea = rows.Should().ContainSingle(r => r.DishId == DishA && r.AreaKey != "*").Subject;
        perArea.SampleSize.Should().Be(5);
        perArea.MedianAmount.Should().Be(120m); // odd count → middle value
    }

    [Fact]
    public void Median_of_even_sample_is_the_mean_of_the_two_middle_values()
    {
        var items = new[]
        {
            Item(DishA, CategoryA, 100m, null, null),
            Item(DishA, CategoryA, 200m, null, null),
            Item(DishA, CategoryA, 300m, null, null),
            Item(DishA, CategoryA, 500m, null, null),
        };

        var rows = PriceMedianCalculator.Calculate(items, areaSampleThreshold: 5);

        // (200 + 300) / 2 = 250 for the city-wide row.
        rows.Should().ContainSingle(r => r.DishId == DishA && r.AreaKey == "*")
            .Which.MedianAmount.Should().Be(250m);
    }

    [Fact]
    public void Items_without_coordinates_contribute_only_to_the_city_wide_median()
    {
        var items = new[]
        {
            // Enough geocoded samples in one area for a per-area row.
            Item(DishA, CategoryA, 100m, LatCellA, LngCellA),
            Item(DishA, CategoryA, 100m, LatCellA, LngCellA),
            Item(DishA, CategoryA, 100m, LatCellA, LngCellA),
            Item(DishA, CategoryA, 100m, LatCellA, LngCellA),
            Item(DishA, CategoryA, 100m, LatCellA, LngCellA),
            // Un-geocoded outlier: counts toward city-wide only, never an area.
            Item(DishA, CategoryA, 900m, null, null),
        };

        var rows = PriceMedianCalculator.Calculate(items, areaSampleThreshold: 5);

        var perArea = rows.Single(r => r.DishId == DishA && r.AreaKey != "*");
        perArea.SampleSize.Should().Be(5);
        perArea.MedianAmount.Should().Be(100m); // outlier excluded from the area

        var cityWide = rows.Single(r => r.DishId == DishA && r.AreaKey == "*");
        cityWide.SampleSize.Should().Be(6); // outlier included city-wide
    }

    [Fact]
    public void Rejects_a_threshold_below_one()
    {
        var act = () => PriceMedianCalculator.Calculate(Array.Empty<PricedMenuItem>(), areaSampleThreshold: 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static PricedMenuItem Item(Guid dishId, Guid categoryId, decimal price, double? lat, double? lng)
        => new(lat, lng, dishId, categoryId, price, "UAH");
}
