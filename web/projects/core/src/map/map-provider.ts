/**
 * The **swappable map-provider interface** (§5.7, design decision #11).
 *
 * The "Глянути на карті / View on map" source sits behind this port so it is **swappable** and
 * **portable to React Native** (the RN app re-implements the same interface with its own native
 * provider). Two implementations live alongside this file:
 *
 * - {@link GoogleEmbedMapProvider} — a live Google Maps **Embed** iframe whose credential comes from
 *   environment config (never hardcoded);
 * - {@link DeepLinkMapProvider} — the graceful degradation: a PlaceId / Maps **deep-link** affordance
 *   used when no Embed credential is present.
 *
 * §5.7 / invariants #6 & #7 hold for every provider: the view is **live**, **nothing is cached**, and
 * no Google rating / Google-sourced coordinate is ever stored — the Google rating is visible only
 * inside the live Embed. A payload with `hasMapData === false` (no stored coordinates) is the
 * provider's "no map data" case and yields a {@link MapView} of kind `'none'`.
 */
import { InjectionToken } from '@angular/core';

import type { MapPayloadDto } from '../domain';

/**
 * The resolved, view-ready representation of a map for one restaurant. A discriminated union so the
 * consumer (the map modal) renders exactly one branch with no `any` and no field guessing:
 *
 * - `'embed'` — a live embeddable map `url` (the Google Embed iframe `src`) the modal renders in a
 *   sandboxed iframe;
 * - `'deep-link'` — a `url` the modal renders as an "open in Google Maps" link (the degraded path,
 *   or whenever only a Place ID / deep-link is available);
 * - `'none'` — there is no map data for this restaurant (`hasMapData === false` / no usable fields);
 *   the modal renders the well-defined empty state.
 */
export type MapView =
  | { readonly kind: 'embed'; readonly url: string }
  | { readonly kind: 'deep-link'; readonly url: string }
  | { readonly kind: 'none' };

/**
 * A map source. Given the live, never-cached {@link MapPayloadDto} from `GET /map/{restaurantId}`,
 * returns the view-ready {@link MapView}. Implementations are pure (no I/O, no caching) — the payload
 * is fetched by the data-access port; the provider only **shapes** it for the view.
 */
export interface MapProvider {
  /** Resolve the view-ready map for the given live payload. */
  resolve(payload: MapPayloadDto): MapView;
}

/**
 * DI token for the **active** map provider. Its factory (wired in {@link provideMapProvider}) selects
 * the Google-Embed provider when an Embed credential is configured and the deep-link provider
 * otherwise, so feature code injects one stable port and never knows which implementation backs it.
 */
export const MAP_PROVIDER = new InjectionToken<MapProvider>('MAP_PROVIDER');
