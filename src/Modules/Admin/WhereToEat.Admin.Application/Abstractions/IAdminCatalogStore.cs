using WhereToEat.SharedKernel.Results;

namespace WhereToEat.Admin.Application.Abstractions;

/// <summary>
/// The admin module's write port over the existing SQL-script-owned schema (Steps 4–5). The
/// EF-backed adapter in <c>Admin.Infrastructure</c> implements it <b>database-first</b> (EF maps to
/// the exact tables/columns the scripts created; EF never alters the schema). Keeping the use-cases
/// behind this port means the Application layer holds no EF dependency (dependency-direction rule b)
/// and the handlers stay unit-testable with a fake store.
///
/// <para>
/// EF is the ONLY data-access stack the Admin module uses — and the only place EF is allowed in the
/// whole solution (the Step 2 EF-containment rule). The admin CRUD surface is low-traffic, so the
/// productivity of EF's change-tracking outweighs the hand-tuned-SQL discipline used on the hot
/// read paths elsewhere.
/// </para>
/// </summary>
public interface IAdminCatalogStore
{
    /// <summary>
    /// Edits a menu item's price (amount + ISO currency), optional weight, and the dish it belongs to
    /// (re-categorisation = pointing the item at a dish in the target category). Marks the item as
    /// admin-sourced provenance so a later parse treats it as curated. Fails if the item is unknown,
    /// the target dish is unknown, or the price is invalid.
    /// </summary>
    Task<Result> EditMenuItemAsync(
        Guid menuItemId,
        Guid dishId,
        decimal priceAmount,
        string priceCurrency,
        string? weight,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the per-MenuItem <c>DoNotParse</c> flag (invariant #3): when on, the parser must not
    /// create/overwrite this item. Fails if the item is unknown.
    /// </summary>
    Task<Result> SetMenuItemDoNotParseAsync(
        Guid menuItemId,
        bool doNotParse,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the restaurant-level <c>DoNotUpdate</c> flag (invariant #3): when on, the parser must
    /// skip the whole venue. Upserts the protection row keyed by the restaurant id. Fails if the
    /// restaurant is unknown.
    /// </summary>
    Task<Result> SetRestaurantDoNotUpdateAsync(
        Guid restaurantId,
        bool doNotUpdate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Edits a restaurant's address (street line + optional city). Returns success only when the row
    /// existed and was updated; the caller then raises the re-geocode flow. Fails if the restaurant
    /// is unknown or the line is blank.
    /// </summary>
    Task<Result> EditAddressAsync(
        Guid restaurantId,
        string addressLine,
        string? city,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Toggles a venue's real (non-generic) photo behind the Step 5 permission gate: a real photo is
    /// only displayable when <paramref name="permissionGranted"/> is true (invariant #8). Generic
    /// category photos are our own content and are never affected. Fails if the photo is unknown or
    /// the photo is a generic one (a generic photo never carries a granted permission).
    /// </summary>
    Task<Result> SetRealPhotoPermissionAsync(
        Guid photoId,
        bool permissionGranted,
        CancellationToken cancellationToken = default);
}
