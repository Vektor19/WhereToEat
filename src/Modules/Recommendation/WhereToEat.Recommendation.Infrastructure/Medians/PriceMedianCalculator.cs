namespace WhereToEat.Recommendation.Infrastructure.Medians;

/// <summary>
/// A priced menu item the median calculation consumes: the offering restaurant's stored coordinates
/// (null until geocoded), the selection it counts toward (a dish OR its category — the two-level
/// taxonomy, invariant #2), and the price + ISO currency. The Step 12 nightly job reads one of these
/// per <c>catalog.MenuItem</c> (joined to its restaurant's <c>Location</c> and dish→category) and
/// feeds them to <see cref="PriceMedianCalculator"/>.
/// </summary>
public sealed record PricedMenuItem(
    double? RestaurantLatitude,
    double? RestaurantLongitude,
    Guid DishId,
    Guid CategoryId,
    decimal PriceAmount,
    string Currency);

/// <summary>
/// One precomputed median row destined for the Step 7 <c>recommendation.PriceMedian</c> table — the
/// exact shape <see cref="DbPriceMedianProvider"/> reads back. Exactly one of <see cref="DishId"/> /
/// <see cref="CategoryId"/> is populated (per the table's one-selection check constraint).
/// </summary>
public sealed record PriceMedianRow(
    string AreaKey,
    Guid? DishId,
    Guid? CategoryId,
    decimal MedianAmount,
    string Currency,
    int SampleSize);

/// <summary>
/// Recomputes the per-area / per-category price medians (CLAUDE.md §6 <c>f_price</c>) from the current
/// menu prices, applying the <b>city-wide fallback below threshold N</b>: a per-area row is written for
/// a (area, selection) only when that area has at least <c>N</c> samples; the city-wide ('*') row is
/// always written for every selection, so a sparse area still has a market reference (the same row the
/// DB-only provider falls back to). The result is exactly the rows the Step 7 <c>DbPriceMedianProvider</c>
/// reads — same <see cref="AreaKey"/> grid, same one-selection shape.
/// <para>
/// Pure, I/O-free, deterministic: the writer (<see cref="PriceMedianWriter"/>) handles the DB read of
/// inputs and the upsert of these rows, so this class can be unit-tested in isolation. Items with no
/// restaurant coordinates contribute to the city-wide medians only (they have no resolvable area).
/// </para>
/// </summary>
public static class PriceMedianCalculator
{
    /// <summary>
    /// The default minimum per-area sample size N below which a finer area gets no row and callers
    /// fall back to the city-wide median. Configurable per run via <see cref="Calculate"/>.
    /// </summary>
    public const int DefaultAreaSampleThreshold = 5;

    /// <summary>
    /// Computes the median rows from <paramref name="items"/> using the per-area threshold
    /// <paramref name="areaSampleThreshold"/> (city-wide fallback below it). Medians are computed per
    /// (area, dish) and (area, category), plus the always-present city-wide ('*') rows per selection.
    /// </summary>
    public static IReadOnlyList<PriceMedianRow> Calculate(
        IReadOnlyCollection<PricedMenuItem> items,
        int areaSampleThreshold = DefaultAreaSampleThreshold)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (areaSampleThreshold < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(areaSampleThreshold), areaSampleThreshold, "The per-area sample threshold N must be >= 1.");
        }

        if (items.Count == 0)
        {
            return Array.Empty<PriceMedianRow>();
        }

        var rows = new List<PriceMedianRow>();

        // --- Dish rows --------------------------------------------------------------------------
        AddSelectionRows(
            items,
            keySelector: item => item.DishId,
            rowFactory: (areaKey, selectionId, median, currency, sample) =>
                new PriceMedianRow(areaKey, DishId: selectionId, CategoryId: null, median, currency, sample),
            areaSampleThreshold,
            rows);

        // --- Category rows ----------------------------------------------------------------------
        AddSelectionRows(
            items,
            keySelector: item => item.CategoryId,
            rowFactory: (areaKey, selectionId, median, currency, sample) =>
                new PriceMedianRow(areaKey, DishId: null, CategoryId: selectionId, median, currency, sample),
            areaSampleThreshold,
            rows);

        return rows;
    }

    // Groups the items by selection (dish or category) and emits: one city-wide ('*') row per
    // selection (always), and one per-area row per (area, selection) whose sample size >= N.
    private static void AddSelectionRows(
        IReadOnlyCollection<PricedMenuItem> items,
        Func<PricedMenuItem, Guid> keySelector,
        Func<string, Guid, decimal, string, int, PriceMedianRow> rowFactory,
        int areaSampleThreshold,
        List<PriceMedianRow> sink)
    {
        foreach (var selectionGroup in items.GroupBy(keySelector))
        {
            var selectionId = selectionGroup.Key;
            var selectionItems = selectionGroup.ToList();

            // The city-wide row spans every priced item for this selection (incl. un-geocoded ones),
            // so a venue with no area still has a market reference (the threshold-N fallback target).
            var cityWidePrices = selectionItems.Select(i => i.PriceAmount).ToList();
            sink.Add(rowFactory(
                RecommendationSql.CityWideAreaKey,
                selectionId,
                Median(cityWidePrices),
                ResolveCurrency(selectionItems),
                cityWidePrices.Count));

            // Per-area rows: only items with resolvable coordinates have an area; only areas with
            // enough samples get a finer row (below N, callers fall back to the city-wide row above).
            var perArea = selectionItems
                .Where(i => i.RestaurantLatitude is not null && i.RestaurantLongitude is not null)
                .GroupBy(i => AreaKey.FromCoordinates(i.RestaurantLatitude!.Value, i.RestaurantLongitude!.Value));

            foreach (var areaGroup in perArea)
            {
                var areaItems = areaGroup.ToList();
                if (areaItems.Count < areaSampleThreshold)
                {
                    continue;
                }

                var areaPrices = areaItems.Select(i => i.PriceAmount).ToList();
                sink.Add(rowFactory(
                    areaGroup.Key,
                    selectionId,
                    Median(areaPrices),
                    ResolveCurrency(areaItems),
                    areaPrices.Count));
            }
        }
    }

    // The statistical median of a non-empty price set: the middle value for an odd count, the mean of
    // the two middle values for an even count. Rounded to 2 dp to match the table's DECIMAL(18,2).
    private static decimal Median(List<decimal> prices)
    {
        prices.Sort();
        var count = prices.Count;
        var mid = count / 2;

        var median = (count % 2) == 1
            ? prices[mid]
            : (prices[mid - 1] + prices[mid]) / 2m;

        return decimal.Round(median, 2, MidpointRounding.AwayFromZero);
    }

    // A median basket is meaningful only within one currency; the catalog stores a single currency per
    // item, so we take the dominant (most frequent) currency in the sample as the row's currency.
    private static string ResolveCurrency(IReadOnlyCollection<PricedMenuItem> items)
    {
        return items
            .GroupBy(i => i.Currency, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => g.Key)
            .First()
            .ToUpperInvariant();
    }
}
