namespace WhereToEat.Contracts.Recommendation;

/// <summary>
/// The public recommendation request shape from CLAUDE.md §6:
/// <c>{ items, match, sort, filters, userGeo }</c>. Search stays deterministic
/// (invariant #1) — <see cref="Items"/> are explicit category/dish selections the user
/// picked from lists, never free text. <see cref="Match"/> and <see cref="Sort"/> are
/// string keys resolved to pluggable strategies by the engine (invariant #4), so this
/// contract does not need to change when new modes are added.
/// </summary>
public sealed record RecommendationRequest(
    IReadOnlyList<SelectedItem> Items,
    string Match,
    string Sort,
    IReadOnlyList<FilterSelection> Filters,
    UserGeo? UserGeo);

/// <summary>
/// One selected catalog item — either a whole category or a specific dish (the two-level
/// taxonomy, invariant #2). <b>Exactly one</b> of the ids is populated; the invalid states
/// (neither / both) are unreachable because the primary constructor is private and the only
/// way to build one is via <see cref="Category"/> or <see cref="Dish"/>.
/// </summary>
public sealed record SelectedItem
{
    // Private primary constructor: callers must use the Category/Dish factories, which is
    // the only place the "exactly one id" invariant can be enforced (Contracts is
    // framework-free, so the factory throws on a programming error rather than returning a Result).
    private SelectedItem(Guid? categoryId, Guid? dishId)
    {
        CategoryId = categoryId;
        DishId = dishId;
    }

    /// <summary>
    /// The selected category id, or <c>null</c> when this selection is a dish. Get-only (no
    /// <c>init</c>) so a <c>with</c> expression cannot reopen the "neither / both" invalid states.
    /// </summary>
    public Guid? CategoryId { get; }

    /// <summary>
    /// The selected dish id, or <c>null</c> when this selection is a category. Get-only (no
    /// <c>init</c>) so a <c>with</c> expression cannot reopen the "neither / both" invalid states.
    /// </summary>
    public Guid? DishId { get; }

    /// <summary>Selects a whole category (the dish id stays null).</summary>
    /// <exception cref="ArgumentException"><paramref name="categoryId"/> is empty.</exception>
    public static SelectedItem Category(Guid categoryId)
    {
        if (categoryId == Guid.Empty)
        {
            throw new ArgumentException("A category selection requires a non-empty category id.", nameof(categoryId));
        }

        return new SelectedItem(categoryId, null);
    }

    /// <summary>Selects a specific dish (the category id stays null).</summary>
    /// <exception cref="ArgumentException"><paramref name="dishId"/> is empty.</exception>
    public static SelectedItem Dish(Guid dishId)
    {
        if (dishId == Guid.Empty)
        {
            throw new ArgumentException("A dish selection requires a non-empty dish id.", nameof(dishId));
        }

        return new SelectedItem(null, dishId);
    }
}

/// <summary>A composable filter selection (price, rating, future open-now/vegan/…), by key.</summary>
public sealed record FilterSelection(string Key, string? Value);

/// <summary>
/// The user's approximate location for distance ranking. Optional: distance is on by
/// default but the user may omit/disable it to range further for price or quality.
/// </summary>
public sealed record UserGeo(double Latitude, double Longitude);
