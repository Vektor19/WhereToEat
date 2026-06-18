namespace WhereToEat.Admin.Infrastructure.Entities;

/// <summary>
/// The Admin module's OWN EF entity over the existing <c>catalog.MenuItem</c> table (Step 4). Maps
/// database-first onto the columns the admin CRUD touches: the dish it belongs to, the
/// <c>Money</c> price (amount + ISO currency), weight, the provenance <c>Source</c> (set to the
/// admin value on edit), and the per-item <c>DoNotParse</c> protection flag (invariant #3). The
/// <c>RawSnapshot</c> column is left unmapped (parser-owned).
/// </summary>
public sealed class MenuItemEntity
{
    /// <summary>The menu item id (the shared <c>catalog.MenuItem.Id</c> Guid PK).</summary>
    public Guid Id { get; set; }

    /// <summary>The owning restaurant.</summary>
    public Guid RestaurantId { get; set; }

    /// <summary>The dish this item offers (re-categorisation points this at a dish in another category).</summary>
    public Guid DishId { get; set; }

    /// <summary>The price amount (non-negative; the DB CHECK + domain guard enforce it).</summary>
    public decimal PriceAmount { get; set; }

    /// <summary>The 3-letter ISO 4217 currency.</summary>
    public string PriceCurrency { get; set; } = string.Empty;

    /// <summary>Optional free-form weight/quantity.</summary>
    public string? Weight { get; set; }

    /// <summary>Provenance: 0 = Parsed draft, 1 = Admin source-of-truth (invariant #3).</summary>
    public int Source { get; set; }

    /// <summary>When true, the parser must not create/overwrite this item (invariant #3).</summary>
    public bool DoNotParse { get; set; }
}
