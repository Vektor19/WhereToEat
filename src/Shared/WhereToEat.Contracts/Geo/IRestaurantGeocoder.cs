namespace WhereToEat.Contracts.Geo;

/// <summary>
/// The cross-module seam the <b>Parsing module</b> uses to geocode-and-store a restaurant's
/// coordinates after persisting it — <b>without</b> referencing the Geo module's internals (its
/// <c>IGeocoder</c>/command handler live in <c>Geo.Application</c>, a module-internal assembly Parsing
/// may not reference). The Geo module supplies the adapter over its existing Step 8 geocode flow
/// (<c>GeocodeRestaurantCommandHandler</c> → OSM/Nominatim → the Catalog coordinate-writer seam), so
/// the coupling stays inside Geo and the Step 2 module-isolation rule stays green.
///
/// The result is best-effort: a failed geocode (no match, transport error) does <b>not</b> fail the
/// parse — the restaurant and its menu are still persisted; only the coordinates are missing until a
/// later run. Implementations therefore return a bool rather than throwing.
/// </summary>
public interface IRestaurantGeocoder
{
    /// <summary>
    /// Geocodes the restaurant's current address (read from the catalog through Geo's own seam) and
    /// stores the OSM-sourced coordinates. Returns <c>true</c> when the stored coordinates were
    /// refreshed, <c>false</c> when geocoding produced no usable coordinate (the parse continues).
    /// </summary>
    Task<bool> GeocodeAndStoreAsync(Guid restaurantId, CancellationToken cancellationToken = default);
}
