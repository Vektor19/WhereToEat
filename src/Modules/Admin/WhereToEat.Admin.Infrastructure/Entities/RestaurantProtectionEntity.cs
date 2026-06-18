namespace WhereToEat.Admin.Infrastructure.Entities;

/// <summary>
/// The Admin module's EF entity over the <c>admin.RestaurantProtection</c> table (Step 5) — the
/// restaurant-level <c>DoNotUpdate</c> flag (invariant #3). Keyed by the venue Guid (no cross-module
/// FK; the Admin schema stays independent of Catalog). The admin CRUD upserts this row to shield a
/// hand-curated venue from the parser.
/// </summary>
public sealed class RestaurantProtectionEntity
{
    /// <summary>The venue id (the <c>admin.RestaurantProtection.RestaurantId</c> Guid PK).</summary>
    public Guid RestaurantId { get; set; }

    /// <summary>When true, the parser must skip the whole venue (invariant #3).</summary>
    public bool DoNotUpdate { get; set; }
}
