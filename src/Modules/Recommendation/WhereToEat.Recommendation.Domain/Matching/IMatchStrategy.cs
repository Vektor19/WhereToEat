using WhereToEat.SharedKernel.ValueObjects;

namespace WhereToEat.Recommendation.Domain.Matching;

/// <summary>
/// Step 1 of the pipeline (CLAUDE.md §6): the <b>match predicate</b> — a pluggable strategy that
/// decides whether a candidate qualifies for the user's selection and how its basket price is
/// computed. <c>OR</c> keeps venues with ≥1 selected item; <c>AND</c> (the combo search) keeps only
/// venues with all selected items; future variants (e.g. "K-of-N") plug in as new keyed strategies
/// without touching the pipeline or the existing ones (invariant #4 — pluggable engine).
/// <para>
/// The strategy operates on a candidate's <b>already-matched items</b> (the data layer narrowed by
/// dish/category at the SQL level); the strategy applies the count-of-N rule and computes the
/// representative basket price (cheapest matched for OR, sum for AND) — this keeps the match
/// semantics in one pluggable place rather than scattered across the data layer.
/// </para>
/// </summary>
public interface IMatchStrategy
{
    /// <summary>
    /// The request key this strategy answers to (e.g. <c>"or"</c>, <c>"and"</c>). Resolved from the
    /// request's <c>match</c> field via DI, so a new predicate self-registers under a new key.
    /// </summary>
    string Key { get; }

    /// <summary>
    /// Decides whether <paramref name="candidate"/> qualifies for the full
    /// <paramref name="selectionCount"/> the user picked, and if so the representative basket price.
    /// Returns <c>null</c> when the candidate does not satisfy the predicate (it is dropped).
    /// </summary>
    /// <param name="candidate">The candidate and its matched items.</param>
    /// <param name="selectionCount">How many distinct items the user selected (the "N").</param>
    Money? Qualify(RestaurantCandidate candidate, int selectionCount);
}
