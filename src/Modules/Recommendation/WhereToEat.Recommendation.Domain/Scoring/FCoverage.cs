namespace WhereToEat.Recommendation.Domain.Scoring;

/// <summary>
/// <c>f_coverage</c> — how many of the selected items a venue actually has (CLAUDE.md §6): coverage
/// normalized by the selection count (3-of-3 → 1, 1-of-3 → 0.33). In a <b>composite</b> mode this is
/// a small doweight that breaks near-ties in favour of the venue offering more of what you wanted;
/// under an <b>explicit</b> sort it is only a tie-breaker, never the primary key (invariant #5 — the
/// explicit-sort strategy enforces that, not this function). With AND match, coverage is always full,
/// so this contributes a constant 1 and does not distort the combo ranking.
/// </summary>
public sealed class FCoverage : IScoringFunction
{
    /// <summary>The factor key for the coverage function.</summary>
    public const string FactorKey = "coverage";

    /// <inheritdoc />
    public string Key => FactorKey;

    /// <inheritdoc />
    public double Score(AggregatedCandidate candidate, ScoringContext context)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(context);

        // No selection (degenerate) => coverage is meaningless; neutral full score.
        if (context.MaxCoverage <= 0)
        {
            return 1d;
        }

        var normalized = (double)candidate.Coverage / context.MaxCoverage;
        return Normalization.Clamp01(normalized);
    }
}
