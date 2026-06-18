using WhereToEat.SharedKernel.ValueObjects;

namespace WhereToEat.Recommendation.Domain.Matching;

/// <summary>
/// The <c>AND</c> match predicate (CLAUDE.md §6 Step 1) — the <b>combo search</b>: a venue qualifies
/// only if it has <b>all</b> selected items ("Пюре і Шніцель" — the combo, not either part alone).
/// The representative basket price is the <b>sum</b> of the matched items (you buy the whole combo).
/// Because "all present" is the gate here, coverage is <b>not</b> a ranking factor under AND — every
/// qualifying venue has full coverage by definition (CLAUDE.md §6 Step 3 note).
/// </summary>
public sealed class AndMatchStrategy : IMatchStrategy
{
    /// <summary>The DI/request key for the AND predicate.</summary>
    public const string MatchKey = "and";

    /// <inheritdoc />
    public string Key => MatchKey;

    /// <inheritdoc />
    public Money? Qualify(RestaurantCandidate candidate, int selectionCount)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        // AND gate: the venue must cover every selected item. The candidate source matches at most
        // one item per distinct selection, so coverage == selectionCount means "all present".
        if (selectionCount <= 0 || candidate.MatchedItems.Count < selectionCount)
        {
            return null;
        }

        // Combo basket price = the sum of the matched items. All prices share a currency in this
        // catalog (UAH); Money.Add throws on a mismatch, which would be a data-integrity bug.
        var total = candidate.MatchedItems[0].Price;
        for (var i = 1; i < candidate.MatchedItems.Count; i++)
        {
            total = total.Add(candidate.MatchedItems[i].Price);
        }

        return total;
    }
}
