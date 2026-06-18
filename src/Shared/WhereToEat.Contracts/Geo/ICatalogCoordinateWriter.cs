namespace WhereToEat.Contracts.Geo;

/// <summary>
/// The cross-module seam the <b>Geo module</b> uses to read a restaurant's address-to-geocode and to
/// write back the freshly geocoded coordinates — <b>without</b> referencing the Catalog module's
/// internals. Geo depends only on this <c>Contracts</c> port; the Catalog module supplies the adapter
/// (over its own <c>ICatalogRepository</c>), so the catalog-domain coupling stays inside the Catalog
/// module and the Step 2 module-isolation fitness rule stays green (modules talk only through
/// <c>WhereToEat.Contracts</c>).
///
/// The write path accepts only an OSM/Nominatim-sourced <see cref="GeoCoordinatesDto"/>; the Catalog
/// adapter rebuilds its OSM-only <c>Coordinates</c> value object from it, so there is no code path by
/// which a Google coordinate could be stored (invariant #7).
/// </summary>
public interface ICatalogCoordinateWriter
{
    /// <summary>
    /// Returns the geocoding input for a restaurant — the single address line the OSM/Nominatim
    /// geocoder takes (city appended when known) — or <c>null</c> when no such restaurant exists.
    /// </summary>
    Task<string?> GetGeocodingAddressAsync(Guid restaurantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns up to <paramref name="maxCount"/> restaurant ids that have an address but <b>no stored
    /// coordinates yet</b> — the queued geocode work the Step 12 geocode-refresh job drains (so a newly
    /// parsed venue that was never geocoded gets coordinates on the next run). Ordered deterministically
    /// so a capped batch is stable across runs; the job is idempotent (a geocoded venue drops out of the
    /// next batch). This is the catalog-owned read of "needs geocode"; the Catalog adapter implements
    /// it over its own repository so Geo never touches Catalog internals.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetRestaurantIdsNeedingGeocodeAsync(
        int maxCount,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists the OSM-sourced <paramref name="coordinates"/> as the restaurant's stored coordinates
    /// (the SQL <c>geography</c> value). Returns <c>false</c> when no such restaurant exists; the
    /// coordinates are rejected (returning <c>false</c>) if their latitude/longitude are out of range.
    /// </summary>
    Task<bool> SetCoordinatesAsync(
        Guid restaurantId,
        GeoCoordinatesDto coordinates,
        CancellationToken cancellationToken = default);
}
