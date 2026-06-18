using WhereToEat.SharedKernel.Geo;
using WhereToEat.SharedKernel.ValueObjects;

namespace WhereToEat.Geo.Application;

/// <summary>
/// The Geo module's thin distance facade for display/ranking: it reuses the framework-free
/// <see cref="Haversine"/> math from the SharedKernel (invariant #7 — "X km from you" computed in
/// code, no paid geo API) so callers in this module have a single, named entry point rather than
/// reaching into the SharedKernel directly. Pure and side-effect-free.
/// </summary>
public static class GeoDistance
{
    /// <summary>
    /// Returns the great-circle distance in kilometres between <paramref name="from"/> and
    /// <paramref name="to"/> via the shared <see cref="Haversine"/> formula.
    /// </summary>
    public static double KilometresBetween(GeoPoint from, GeoPoint to) => Haversine.DistanceKm(from, to);
}
