namespace WhereToEat.Recommendation.Domain;

/// <summary>
/// Per-mode <b>normalization</b> tuning for the scoring functions (CLAUDE.md §6 Step 3).
/// The <see cref="DistanceNormalizationRadiusKm"/> sets the radius over which <c>f_distance</c> falls
/// from 1 to 0; <see cref="MaxNormalizedRating"/> is the rating scale (5★) <c>f_quality</c> normalizes
/// against; <see cref="NeutralNormalizedQuality"/> is the fallback for an unrated venue.
/// <para>
/// The composite-mode <b>weights</b> are deliberately <b>not</b> here: they live solely in the
/// DI-injected per-factor weight map that <see cref="Sorting.CompositeScoreStrategy"/> reads (keyed by
/// each <c>f_*</c>'s key), so there is one authoritative representation of a mode's weights — this
/// options type only carries the normalization inputs the <c>f_*</c> functions themselves need.
/// </para>
/// <para>
/// These are pure configuration values consumed by the Domain scoring math — no I/O. A host binds
/// them from configuration; the defaults below are sensible starting points.
/// </para>
/// </summary>
public sealed record RecommendationModeOptions
{
    /// <summary>
    /// The radius (km) over which <c>f_distance</c> decays linearly from 1 (at the user) to 0
    /// (at/over the radius). Beyond it a venue scores 0 on distance but is not excluded.
    /// </summary>
    public double DistanceNormalizationRadiusKm { get; init; } = 10d;

    /// <summary>The maximum rating on the scale (5★) used to normalize <c>f_quality</c> into 0…1.</summary>
    public double MaxNormalizedRating { get; init; } = 5d;

    /// <summary>
    /// The neutral quality used when a venue has no rating yet (so an unrated venue is not treated
    /// as the worst possible). Expressed on the same 0…1 normalized scale as <c>f_quality</c>.
    /// </summary>
    public double NeutralNormalizedQuality { get; init; } = 0.5d;

    /// <summary>Validates the options (radius/scale positive and finite, neutral quality in [0, 1]).</summary>
    public void Validate()
    {
        if (DistanceNormalizationRadiusKm <= 0d || !double.IsFinite(DistanceNormalizationRadiusKm))
        {
            throw new InvalidOperationException("DistanceNormalizationRadiusKm must be a positive, finite number of km.");
        }

        if (MaxNormalizedRating <= 0d || !double.IsFinite(MaxNormalizedRating))
        {
            throw new InvalidOperationException("MaxNormalizedRating must be a positive, finite rating scale.");
        }

        if (NeutralNormalizedQuality is < 0d or > 1d)
        {
            throw new InvalidOperationException("NeutralNormalizedQuality must be in [0, 1].");
        }
    }
}
