namespace WhereToEat.Admin.Infrastructure.Entities;

/// <summary>
/// The Admin module's OWN EF entity over the existing <c>catalog.Photo</c> table (Step 5). Admin CRUD
/// toggles a real photo's permission gate (invariant #8): a real photo (<see cref="IsGeneric"/> =
/// false) is displayable only when <see cref="PermissionGranted"/> is true; our own generic photos
/// are never permission-gated. Not the Catalog domain's <c>Photo</c> (module-isolation rule d).
/// </summary>
public sealed class PhotoEntity
{
    /// <summary>The photo id (the shared <c>catalog.Photo.Id</c> Guid PK).</summary>
    public Guid Id { get; set; }

    /// <summary>The dish this photo illustrates.</summary>
    public Guid DishId { get; set; }

    /// <summary>True for our own generic category illustration (the default, never permission-gated).</summary>
    public bool IsGeneric { get; set; }

    /// <summary>For a real photo, whether the venue granted permission to display it (invariant #8).</summary>
    public bool PermissionGranted { get; set; }

    /// <summary>The photo URL.</summary>
    public string Url { get; set; } = string.Empty;
}
