namespace WhereToEat.Recommendation.Domain.Scoring;

/// <summary>
/// Small shared helpers for the scoring functions so every <c>f_*</c> normalizes the same way. Pure
/// math; clamps keep every facet strictly in 0…1 so the composite score stays bounded and the
/// weights stay comparable across modes (CLAUDE.md §6 — each <c>f_*</c> normalized to 0…1).
/// </summary>
internal static class Normalization
{
    /// <summary>Clamps <paramref name="value"/> into [0, 1]; maps a non-finite value to 0.</summary>
    public static double Clamp01(double value)
    {
        if (!double.IsFinite(value))
        {
            return 0d;
        }

        return value switch
        {
            < 0d => 0d,
            > 1d => 1d,
            _ => value,
        };
    }
}
