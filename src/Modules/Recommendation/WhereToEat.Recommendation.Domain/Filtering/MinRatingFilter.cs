using System.Globalization;

namespace WhereToEat.Recommendation.Domain.Filtering;

/// <summary>
/// The <b>min-rating</b> filter (CLAUDE.md §6 Step 4): keeps only venues whose <b>smoothed</b>
/// all-time rating (invariant #6 — our own rating, never Google) is at or above the requested floor.
/// A venue with no rating yet is <b>dropped</b> when a floor is set (it has not earned the bar). A
/// malformed/negative floor is ignored (the filter no-ops, keeping every candidate).
/// </summary>
public sealed class MinRatingFilter : IResultFilter
{
    /// <summary>The request/DI key for the rating filter.</summary>
    public const string FilterKey = "rating";

    /// <inheritdoc />
    public string Key => FilterKey;

    /// <inheritdoc />
    public bool Keep(AggregatedCandidate candidate, string? value)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var minRating)
            || minRating < 0d)
        {
            // No usable floor => the filter does not apply; keep the candidate.
            return true;
        }

        // No rating cannot clear a positive floor; an unrated venue is dropped when a floor is set.
        return candidate.SmoothedRating is { } rating && rating >= minRating;
    }
}
