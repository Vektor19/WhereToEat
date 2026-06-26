/**
 * Haversine great-circle distance (Step 8) — the TypeScript mirror of the backend's
 * `WhereToEat.SharedKernel.Geo.Haversine`. This is invariant #7/#11's local "X km from you" math:
 * computed in code, no paid geo API, our own geo.
 *
 * **Display only.** The backend *ranks* by distance; the recommend DTO already carries `distanceKm`
 * for the result list. This helper covers the rare client-side display case (e.g. a detail view
 * computing distance from the user to a single venue) and keeps the formula identical to the
 * backend's so a client-computed distance never disagrees with the server's. Pure and framework-free
 * — mirrored 1:1 by the RN rewrite.
 */

/**
 * A latitude/longitude point in decimal degrees (WGS-84). Structurally matches the domain `UserGeo`,
 * so a `UserGeo` value is accepted directly without an import that would couple this pure helper to
 * the domain barrel.
 */
export interface GeoCoordinate {
  readonly latitude: number;
  readonly longitude: number;
}

/** Mean Earth radius in kilometres (IUGG mean radius R1) — identical to the backend constant. */
export const EARTH_RADIUS_KM = 6371.0088;

function degreesToRadians(degrees: number): number {
  return degrees * (Math.PI / 180);
}

/**
 * Great-circle distance in kilometres between `from` and `to`. Distance from a point to itself is
 * exactly `0` (an identity short-circuit avoids floating-point residue, matching the backend).
 */
export function haversineDistanceKm(from: GeoCoordinate, to: GeoCoordinate): number {
  if (from.latitude === to.latitude && from.longitude === to.longitude) {
    return 0;
  }

  const lat1 = degreesToRadians(from.latitude);
  const lat2 = degreesToRadians(to.latitude);
  const deltaLat = degreesToRadians(to.latitude - from.latitude);
  const deltaLon = degreesToRadians(to.longitude - from.longitude);

  const sinHalfDeltaLat = Math.sin(deltaLat / 2);
  const sinHalfDeltaLon = Math.sin(deltaLon / 2);

  const a =
    sinHalfDeltaLat * sinHalfDeltaLat +
    Math.cos(lat1) * Math.cos(lat2) * sinHalfDeltaLon * sinHalfDeltaLon;

  const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));

  return EARTH_RADIUS_KM * c;
}
