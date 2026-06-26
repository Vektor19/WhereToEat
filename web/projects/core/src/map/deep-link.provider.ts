/**
 * The **deep-link** map provider (§5.7, design decision #11) — the graceful degradation used when no
 * Google Maps Embed credential is configured.
 *
 * Instead of a broken/empty embed, it offers an **"open in Google Maps" deep-link** built only from
 * the fields we store (in priority order): the backend-supplied {@link MapPayloadDto.mapsDeepLink},
 * else a Place-ID Maps URL, else an OSM-coordinate Maps URL. When the payload has no map data
 * (`hasMapData === false`) or none of those fields, it returns the `'none'` view so the modal shows
 * the well-defined empty state.
 *
 * **Nothing is cached and no Google data is stored** (invariants #6/#7): this provider is pure — it
 * only shapes the live payload the data layer just fetched into a `'deep-link'` / `'none'` view.
 */
import { Injectable } from '@angular/core';

import type { MapPayloadDto } from '../domain';
import type { MapProvider, MapView } from './map-provider';

/** Base for a Place-ID Google Maps search deep-link (`?query_place_id=`). */
const PLACE_QUERY_BASE = 'https://www.google.com/maps/search/?api=1';

@Injectable({ providedIn: 'root' })
export class DeepLinkMapProvider implements MapProvider {
  resolve(payload: MapPayloadDto): MapView {
    const url = this.buildUrl(payload);
    return url === null ? { kind: 'none' } : { kind: 'deep-link', url };
  }

  /**
   * Pick the best available deep-link from the stored fields, or `null` when none is usable. Order:
   * an explicit backend deep-link wins; then a Place-ID query; then OSM coordinates. A payload with
   * `hasMapData === false` short-circuits to `null` (the "no map data" case).
   */
  private buildUrl(payload: MapPayloadDto): string | null {
    if (!payload.hasMapData) {
      return null;
    }

    const deepLink = payload.mapsDeepLink;
    if (deepLink !== undefined && deepLink !== null && deepLink.length > 0) {
      return deepLink;
    }

    const placeId = payload.placeId;
    if (placeId !== undefined && placeId !== null && placeId.length > 0) {
      return `${PLACE_QUERY_BASE}&query_place_id=${encodeURIComponent(placeId)}`;
    }

    if (hasCoordinates(payload)) {
      const query = `${payload.latitude},${payload.longitude}`;
      return `${PLACE_QUERY_BASE}&query=${encodeURIComponent(query)}`;
    }

    return null;
  }
}

/** Type guard: both coordinate fields are present numbers (our stored OSM lat/lng). */
function hasCoordinates(
  payload: MapPayloadDto,
): payload is MapPayloadDto & { latitude: number; longitude: number } {
  return typeof payload.latitude === 'number' && typeof payload.longitude === 'number';
}
