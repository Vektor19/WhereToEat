namespace WhereToEat.Admin.Infrastructure.Entities;

/// <summary>
/// The Admin module's OWN EF entity over the existing <c>catalog.Dish</c> table (Step 4). Admin CRUD
/// only reads it (to validate that an edited menu item's target dish exists for re-categorisation),
/// so it carries just the id + its category. Not the Catalog domain's <c>Dish</c> (module-isolation
/// rule d).
/// </summary>
public sealed class DishEntity
{
    /// <summary>The dish id (the shared <c>catalog.Dish.Id</c> Guid PK).</summary>
    public Guid Id { get; set; }

    /// <summary>The category this dish belongs to (the two-level taxonomy — invariant #2).</summary>
    public Guid CategoryId { get; set; }

    /// <summary>The canonical normalized dish name.</summary>
    public string CanonicalName { get; set; } = string.Empty;
}
