namespace WhereToEat.Admin.Infrastructure.Entities;

/// <summary>
/// The Admin module's OWN EF entity over the existing <c>catalog.Restaurant</c> table (Step 4). It is
/// deliberately NOT the Catalog domain's <c>Restaurant</c> aggregate — the Admin module never
/// references Catalog internals (module-isolation rule d); it maps database-first onto the shared
/// table with only the columns the admin CRUD touches. The <c>Location</c>/<c>RawSnapshot</c> columns
/// are intentionally unmapped (they are owned by the geocoder/parser flows, not admin CRUD).
/// </summary>
public sealed class RestaurantEntity
{
    /// <summary>The restaurant id (the shared <c>catalog.Restaurant.Id</c> Guid PK).</summary>
    public Guid Id { get; set; }

    /// <summary>The display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The street line used for geocoding (admin-editable).</summary>
    public string AddressLine { get; set; } = string.Empty;

    /// <summary>The city/locality, when known.</summary>
    public string? City { get; set; }
}
