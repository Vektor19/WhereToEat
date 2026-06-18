using WhereToEat.Catalog.Domain.Identifiers;
using WhereToEat.SharedKernel.ValueObjects;

namespace WhereToEat.Catalog.Infrastructure.Persistence;

/// <summary>
/// A nearby restaurant returned by the radius pre-filter: its id, name, OSM-sourced coordinates,
/// and the <b>code-computed</b> Haversine distance (km) from the query origin. This is a read-model
/// projection, not a domain aggregate — the recommendation engine (Step 7) consumes a similar
/// candidate shape.
/// </summary>
internal sealed record NearbyRestaurant(RestaurantId Id, string Name, GeoPoint Location, double DistanceKm);

/// <summary>
/// The geography-backed radius pre-filter (invariant #7). The SQL spatial index narrows candidates
/// to those whose stored <c>geography</c> point lies within a radius; the exact distance and final
/// ordering are then decided in code via <c>Haversine</c> — "the index narrows, Haversine decides".
/// Kept separate from <see cref="WhereToEat.Catalog.Domain.Abstractions.ICatalogRepository"/> (a
/// pure domain port that loads whole aggregates) because this is an infrastructure read model with
/// no aggregate identity.
/// </summary>
internal interface ICatalogSpatialReader
{
    /// <summary>
    /// Returns the restaurants whose stored coordinates lie within <paramref name="radiusKm"/> of
    /// <paramref name="origin"/>, ordered nearest-first by the code-computed Haversine distance.
    /// Restaurants without stored coordinates are excluded.
    /// </summary>
    Task<IReadOnlyList<NearbyRestaurant>> FindWithinRadiusAsync(
        GeoPoint origin,
        double radiusKm,
        CancellationToken cancellationToken = default);
}
