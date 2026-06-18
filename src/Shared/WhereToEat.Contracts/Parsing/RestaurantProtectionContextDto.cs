namespace WhereToEat.Contracts.Parsing;

/// <summary>
/// The admin-protection facts the persist gate must consult <b>before writing</b> (invariant #3,
/// admin &gt; parser): whether the matched restaurant already exists, whether it is flagged
/// <see cref="DoNotUpdate"/> (the whole venue is shielded from the parser), and the canonical
/// <see cref="DishId"/>s of its existing menu items that are flagged <c>DoNotParse</c> (those
/// individual items must never be created/overwritten by the parser).
///
/// The Parsing handler reads this through the <c>ICatalogMenuWriter</c> seam and applies the gate
/// itself, so the protection logic is testable in <c>Parsing.Application</c> with a fake port. The
/// Catalog adapter populates it from the catalog read model + the admin protection table.
/// </summary>
public sealed record RestaurantProtectionContextDto(
    Guid? ExistingRestaurantId,
    bool DoNotUpdate,
    IReadOnlySet<Guid> DoNotParseDishIds);
