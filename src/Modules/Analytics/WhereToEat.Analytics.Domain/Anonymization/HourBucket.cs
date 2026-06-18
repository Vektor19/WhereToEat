using WhereToEat.SharedKernel.Primitives;

namespace WhereToEat.Analytics.Domain.Anonymization;

/// <summary>
/// An <b>hour-truncated</b> timestamp — the "retain, but coarsen" rule for time (§8.1): the event
/// minute/second are dropped so a stored row's time can never pin a user to a precise moment, while
/// the hour-of-day/day-of-week demand patterns the B2B analytics need are preserved. Stored as UTC.
/// Equality is by value.
/// </summary>
public sealed class HourBucket : ValueObject
{
    private HourBucket(DateTimeOffset hourUtc)
    {
        HourUtc = hourUtc;
    }

    /// <summary>The UTC instant truncated to the top of the hour (minutes/seconds/ticks zeroed).</summary>
    public DateTimeOffset HourUtc { get; }

    /// <summary>
    /// Truncates an event time to the top of its UTC hour. There is intentionally no constructor that
    /// keeps minute/second precision — the only way to make an <see cref="HourBucket"/> coarsens time.
    /// </summary>
    public static HourBucket FromInstant(DateTimeOffset instant)
    {
        var utc = instant.ToUniversalTime();
        var truncated = new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, TimeSpan.Zero);
        return new HourBucket(truncated);
    }

    /// <inheritdoc />
    public override string ToString() => HourUtc.ToString("yyyy-MM-ddTHH'Z'", System.Globalization.CultureInfo.InvariantCulture);

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return HourUtc;
    }
}
