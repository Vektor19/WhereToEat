using WhereToEat.Catalog.Domain.Identifiers;
using WhereToEat.SharedKernel.Primitives;
using WhereToEat.SharedKernel.Results;
using WhereToEat.SharedKernel.ValueObjects;

namespace WhereToEat.Catalog.Domain.Restaurants;

/// <summary>
/// The <b>dish-at-restaurant join</b> from the design's ER model: a single <see cref="Taxonomy.Dish"/>
/// offered at one <see cref="Restaurant"/>, with its <see cref="Money"/> price, an optional
/// weight/quantity (e.g. "300 г", "0.5 л"), its <see cref="Restaurants.SourceKind"/> provenance
/// (parsed draft vs admin source-of-truth), and the per-item <see cref="DoNotParse"/> flag that
/// shields an admin-curated item from the parser (invariant #3).
///
/// It is a child entity of the <see cref="Restaurant"/> aggregate (not an aggregate root): it is
/// always created/mutated through the restaurant so the "one menu item per dish" invariant holds.
/// </summary>
public sealed class MenuItem : Entity<MenuItemId>
{
    private MenuItem(MenuItemId id, DishId dishId, Money price, string? weight, SourceKind source, bool doNotParse)
        : base(id)
    {
        DishId = dishId;
        Price = price;
        Weight = weight;
        Source = source;
        DoNotParse = doNotParse;
    }

    /// <summary>The dish this item represents. A restaurant has at most one item per dish.</summary>
    public DishId DishId { get; }

    /// <summary>The non-negative price (the <see cref="Money"/> guard enforces non-negativity).</summary>
    public Money Price { get; private set; }

    /// <summary>The free-form weight/quantity label (e.g. "300 г"), or <c>null</c> when unknown.</summary>
    public string? Weight { get; private set; }

    /// <summary>Where this item came from — parser draft (<see cref="SourceKind.Parsed"/>) or admin.</summary>
    public SourceKind Source { get; private set; }

    /// <summary>
    /// When true, the parser must not create or overwrite this item (invariant #3). Set by an admin
    /// to protect a hand-verified item; the parser persist gate (later steps) reads this.
    /// </summary>
    public bool DoNotParse { get; private set; }

    /// <summary>
    /// Creates a menu item, requiring a real (non-default) <see cref="DishId"/> and a non-null
    /// price. Price non-negativity is already guaranteed by <see cref="Money"/>'s own factory, so a
    /// constructed <see cref="Money"/> is by definition a valid price. The factory is
    /// <c>internal</c> because items are only created through the owning <see cref="Restaurant"/>,
    /// which enforces the one-item-per-dish rule.
    /// </summary>
    internal static Result<MenuItem> Create(
        MenuItemId id,
        DishId dishId,
        Money price,
        string? weight,
        SourceKind source,
        bool doNotParse)
    {
        if (dishId == default)
        {
            return Result.Failure<MenuItem>(
                Error.Validation("MenuItem.DishRequired", "A menu item must reference a dish."));
        }

        if (price is null)
        {
            return Result.Failure<MenuItem>(
                Error.Validation("MenuItem.PriceRequired", "A menu item must have a price."));
        }

        var normalizedWeight = string.IsNullOrWhiteSpace(weight) ? null : weight.Trim();
        return Result.Success(new MenuItem(id, dishId, price, normalizedWeight, source, doNotParse));
    }
}
