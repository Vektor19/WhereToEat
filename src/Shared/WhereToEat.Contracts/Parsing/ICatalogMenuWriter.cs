namespace WhereToEat.Contracts.Parsing;

/// <summary>
/// The cross-module seam the <b>Parsing module</b> uses to persist a parsed-and-normalized menu into
/// the Catalog store — <b>without</b> referencing Catalog internals (the Catalog module supplies the
/// adapter over its own repository + the admin protection table, so the coupling stays inside Catalog
/// and the Step 2 module-isolation rule stays green).
///
/// The seam is split into a <b>read of the admin-protection context</b> and a <b>write of the
/// resolved items</b> so the persist <i>gate</i> (invariant #3, admin &gt; parser) lives in the
/// Parsing handler and is unit-testable: the handler reads the context, decides what may be written
/// (skipping a <c>DoNotUpdate</c> venue entirely and any <c>DoNotParse</c> dish within an updatable
/// one), and only then writes. The writer treats already-present rows as upserts but is only ever
/// handed the items the gate already cleared.
/// </summary>
public interface ICatalogMenuWriter
{
    /// <summary>
    /// Returns the admin-protection context for the restaurant identified by
    /// <paramref name="restaurantName"/> + <paramref name="addressLine"/> (the parser's natural key):
    /// whether it exists, whether it is flagged <c>DoNotUpdate</c>, and which of its existing menu
    /// items (by canonical dish id) are flagged <c>DoNotParse</c>. For a brand-new restaurant the
    /// context reports no existing id, not protected, and an empty do-not-parse set.
    /// </summary>
    Task<RestaurantProtectionContextDto> GetProtectionContextAsync(
        string restaurantName,
        string addressLine,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists the restaurant facts and the <paramref name="resolvedItems"/> the gate has already
    /// cleared (the caller must have excluded <c>DoNotParse</c> dishes and must not call this at all
    /// for a <c>DoNotUpdate</c> venue). Creates the restaurant when new and returns its id; upserts
    /// menu items for an existing one. Items are stored with parser provenance.
    /// </summary>
    Task<Guid> PersistAsync(
        ParsedRestaurantFactsDto restaurant,
        IReadOnlyList<ResolvedMenuItemDto> resolvedItems,
        CancellationToken cancellationToken = default);
}
