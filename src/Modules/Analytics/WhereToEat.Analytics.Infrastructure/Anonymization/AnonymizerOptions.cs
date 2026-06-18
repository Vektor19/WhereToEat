using WhereToEat.Analytics.Domain.Anonymization;

namespace WhereToEat.Analytics.Infrastructure.Anonymization;

/// <summary>
/// Configuration for the anonymization-at-ingest pipeline (§8.1 / invariant #11). The defaults are
/// privacy-safe: a neighbourhood-grade geohash precision (capped at the domain's
/// <see cref="Geohash.MaxPrecision"/>) and a daily salt rotation window.
/// </summary>
public sealed class AnonymizerOptions
{
    /// <summary>
    /// The number of geohash characters retained — the cell size. Defaults to 5 (≈ ±2.4 km,
    /// neighbourhood-grade) and is clamped to <see cref="Geohash.MaxPrecision"/> so it can never become
    /// point-precise. A smaller number is a larger (coarser) cell.
    /// </summary>
    public int GeohashPrecision { get; set; } = 5;

    /// <summary>
    /// The master secret mixed into every salted hash. A non-empty value MUST be supplied outside
    /// Development/Testing — <c>AddAnalyticsModule</c> fails fast on a blank secret in any other
    /// environment, because the actor-id HMAC would otherwise key on the public salt-window index alone
    /// and become brute-forceable, defeating anonymization (invariant #11).
    /// </summary>
    public string MasterSecret { get; set; } = string.Empty;

    /// <summary>
    /// How long a salt stays constant before it rotates. Defaults to one day: stable within a day so
    /// intra-day funnel math works, divergent across days so events can't be linked across windows.
    /// </summary>
    public TimeSpan SaltRotationWindow { get; set; } = TimeSpan.FromDays(1);

    /// <summary>The effective, clamped geohash precision actually used by the encoder.</summary>
    public int EffectiveGeohashPrecision => Math.Clamp(GeohashPrecision, 1, Geohash.MaxPrecision);
}
