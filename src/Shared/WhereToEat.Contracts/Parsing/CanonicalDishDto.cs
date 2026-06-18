namespace WhereToEat.Contracts.Parsing;

/// <summary>
/// The cross-module result of resolving a raw parsed dish name onto the canonical two-level taxonomy
/// (invariant #2): the canonical <see cref="DishId"/> and the <see cref="CategoryId"/> it belongs to.
/// This is the only shape the dish-resolution seam returns across the module boundary — the Parsing
/// module never sees a Catalog <c>Dish</c>/<c>DishId</c> domain type, only this opaque-id DTO. A
/// <c>null</c> resolution (no canonical dish) is what routes a raw item to the quarantine queue.
/// </summary>
public sealed record CanonicalDishDto(Guid DishId, Guid CategoryId);
