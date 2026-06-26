/**
 * The **Google Maps Embed** map provider (§5.7, design decision #11) — the default map source.
 *
 * It builds a **live** Google Maps Embed URL from the credential in {@link APP_CONFIG} (`map
 * .googleMapsEmbedKey`, never hardcoded) plus the fields we store on the live {@link MapPayloadDto}:
 * a Place ID (preferred — `mode=place`) or our OSM coordinates (`mode=view&center=`). The modal
 * renders that URL in a sandboxed iframe, so §5.7 / invariants #6 & #7 hold — the map is **live**,
 * **nothing is cached**, and **no Google rating or coordinate is stored**; the Google rating is
 * visible only inside the live Embed.
 *
 * **Graceful degradation (no broken embed).** When **no Embed credential** is configured, or when the
 * payload lacks the Place-ID / coordinates the embed needs, this provider falls back to the
 * {@link DeepLinkMapProvider} (a PlaceId / Maps deep-link, else the `'none'` empty state) rather than
 * emitting a broken iframe. A payload with `hasMapData === false` always resolves to that fallback's
 * `'none'` view.
 *
 * Pure: it only shapes the live payload the data layer just fetched — it performs no I/O of its own.
 */
import { Injectable, inject } from '@angular/core';

import { APP_CONFIG } from '../config/app-config.token';
import type { MapPayloadDto } from '../domain';
import { DeepLinkMapProvider } from './deep-link.provider';
import type { MapProvider, MapView } from './map-provider';

/** Google Maps Embed API base (the `/embed/v1/place` and `/embed/v1/view` modes). */
const EMBED_BASE = 'https://www.google.com/maps/embed/v1';

@Injectable({ providedIn: 'root' })
export class GoogleEmbedMapProvider implements MapProvider {
  private readonly config = inject(APP_CONFIG);
  private readonly deepLink = inject(DeepLinkMapProvider);

  resolve(payload: MapPayloadDto): MapView {
    const url = this.buildEmbedUrl(payload);
    // No credential, or no embeddable target on the payload → degrade to the deep-link affordance
    // (which itself yields the `'none'` empty state when the payload has no usable map data).
    return url === null ? this.deepLink.resolve(payload) : { kind: 'embed', url };
  }

  /**
   * Build the live Embed URL, or `null` when it cannot (no credential, no map data, or no Place-ID /
   * coordinates to centre on). Place ID is preferred over raw coordinates.
   */
  private buildEmbedUrl(payload: MapPayloadDto): string | null {
    const key = this.config.map.googleMapsEmbedKey;
    if (key.length === 0 || !payload.hasMapData) {
      return null;
    }

    const placeId = payload.placeId;
    if (placeId !== undefined && placeId !== null && placeId.length > 0) {
      const params = new URLSearchParams({ key, q: `place_id:${placeId}` });
      return `${EMBED_BASE}/place?${params.toString()}`;
    }

    if (hasCoordinates(payload)) {
      const params = new URLSearchParams({
        key,
        center: `${payload.latitude},${payload.longitude}`,
      });
      return `${EMBED_BASE}/view?${params.toString()}`;
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
