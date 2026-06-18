using WhereToEat.SharedKernel.ValueObjects;

namespace WhereToEat.Recommendation.Domain.Matching;

/// <summary>
/// The <c>OR</c> match predicate (CLAUDE.md §6 Step 1): a venue qualifies if it has <b>≥1</b> of the
/// selected items ("Піца або Бургер або Шашлик" — any one of them is enough). The representative
/// basket price is the <b>cheapest matched item</b>, since with OR the user buys the single thing
/// they want most cheaply. Coverage (how many of the N are present) is then a ranking signal, not a
/// gate — at most a tie-breaker under explicit sort (invariant #5).
/// </summary>
public sealed class OrMatchStrategy : IMatchStrategy
{
    /// <summary>The DI/request key for the OR predicate.</summary>
    public const string MatchKey = "or";

    /// <inheritdoc />
    public string Key => MatchKey;

    /// <inheritdoc />
    public Money? Qualify(RestaurantCandidate candidate, int selectionCount)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        // ≥1 matched item is the OR gate; a venue with none was already not a candidate, but guard
        // here too so the strategy is correct in isolation.
        if (candidate.MatchedItems.Count == 0)
        {
            return null;
        }

        // Cheapest matched offering is the OR basket price.
        var cheapest = candidate.MatchedItems[0].Price;
        foreach (var item in candidate.MatchedItems)
        {
            if (item.Price.Amount < cheapest.Amount)
            {
                cheapest = item.Price;
            }
        }

        return cheapest;
    }
}
