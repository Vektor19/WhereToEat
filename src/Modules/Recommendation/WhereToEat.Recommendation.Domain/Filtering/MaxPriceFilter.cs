using System.Globalization;

namespace WhereToEat.Recommendation.Domain.Filtering;

/// <summary>
/// The <b>max-price</b> filter (CLAUDE.md §6 Step 4): keeps only venues whose basket amount is at or
/// below the requested ceiling. The value is the ceiling parsed with the invariant culture; a
/// malformed/negative ceiling is ignored (the filter no-ops, keeping every candidate) so bad input
/// never empties the result.
/// </summary>
public sealed class MaxPriceFilter : IResultFilter
{
    /// <summary>The request/DI key for the price filter.</summary>
    public const string FilterKey = "price";

    /// <inheritdoc />
    public string Key => FilterKey;

    /// <inheritdoc />
    public bool Keep(AggregatedCandidate candidate, string? value)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var maxPrice)
            || maxPrice < 0m)
        {
            // No usable ceiling => the filter does not apply; keep the candidate.
            return true;
        }

        return candidate.BasketPrice.Amount <= maxPrice;
    }
}
