/**
 * Geolocation acquisition port (Step 15) — a small injectable seam over the browser Geolocation
 * API. It is the only place the platform geolocation API is touched, so:
 *   - it is **testable/mockable** (component code depends on this service, not on `navigator`);
 *   - it is an **RN-portable seam** — the React Native rewrite swaps the implementation
 *     (`expo-location` / a native module) behind the same `acquire()` contract without touching
 *     the geo opt-in UI;
 *   - it requests **approximate** coordinates only (`enableHighAccuracy: false`) — invariant #11's
 *     opt-in approximate geo; precise tracking is never requested.
 *
 * The service **never auto-acquires**: it only reads location when {@link acquire} is called, which
 * the UI calls **after** the user has explicitly opted in (the consent affordance). Calling
 * `acquire()` triggers the browser's permission prompt; the typed {@link GeolocationResult} folds
 * permission-denied / position-unavailable / timeout / unsupported into a handled `failure` (with a
 * stable i18n message key) so no caller ever has to touch a raw `GeolocationPositionError` or crash.
 *
 * Framework-light: an injectable service with one async method, mirrored 1:1 by the RN rewrite.
 */
import { Injectable } from '@angular/core';

import type { GeoCoordinate } from './haversine';

/** Why a geolocation acquisition failed — the discriminator for a failed {@link GeolocationResult}. */
export type GeolocationFailureReason =
  /** The user denied the location-permission prompt (or has it blocked). */
  | 'permission-denied'
  /** The platform could not determine a position (no signal / hardware unavailable). */
  | 'position-unavailable'
  /** Acquisition took longer than the timeout budget. */
  | 'timeout'
  /** The platform has no Geolocation API at all (e.g. an unsupported/old environment). */
  | 'unsupported';

/** A successful acquisition carrying the approximate coordinates. */
export interface GeolocationSuccess {
  readonly ok: true;
  readonly coordinate: GeoCoordinate;
}

/** A handled failure carrying the typed reason and a stable i18n message key (resolved in Step 17). */
export interface GeolocationFailure {
  readonly ok: false;
  readonly reason: GeolocationFailureReason;
  /** Stable i18n key for the user-facing message — no hardcoded locale string blocks Step 17. */
  readonly messageKey: string;
}

/** The typed outcome of {@link GeolocationService.acquire} — success or a handled failure. */
export type GeolocationResult = GeolocationSuccess | GeolocationFailure;

/** i18n message keys for each failure reason (catalog entries land in Step 17). */
export const GEO_MESSAGE_KEYS: Readonly<Record<GeolocationFailureReason, string>> = {
  'permission-denied': 'consumer.geo.error.permissionDenied',
  'position-unavailable': 'consumer.geo.error.positionUnavailable',
  timeout: 'consumer.geo.error.timeout',
  unsupported: 'consumer.geo.error.unsupported',
};

/** Acquisition timeout budget (ms) — bounded so a hung permission/GPS never blocks the toggle. */
const ACQUIRE_TIMEOUT_MS = 10_000;

/**
 * How long a cached position may be reused (ms). Approximate geo does not need a fresh fix every
 * time, and reusing a recent coarse position avoids a redundant prompt/lookup on a quick re-opt-in.
 */
const MAX_CACHE_AGE_MS = 5 * 60_000;

@Injectable({ providedIn: 'root' })
export class GeolocationService {
  /**
   * Acquire the user's **approximate** location. Resolves with a typed {@link GeolocationResult} —
   * never rejects — so the caller switches on `ok` instead of catching. Must only be called after the
   * user opts in (this is what triggers the browser permission prompt).
   *
   * Requests low accuracy on purpose (invariant #11): we want a coarse "you are near here", not
   * precise tracking. Permission-denied / unavailable / timeout / unsupported all map to a handled
   * failure with a stable i18n message key.
   */
  acquire(): Promise<GeolocationResult> {
    const geo = this.geolocation();
    if (geo === null) {
      return Promise.resolve(this.failure('unsupported'));
    }

    return new Promise<GeolocationResult>((resolve) => {
      geo.getCurrentPosition(
        (position) =>
          resolve({
            ok: true,
            coordinate: {
              latitude: position.coords.latitude,
              longitude: position.coords.longitude,
            },
          }),
        (error) => resolve(this.failure(this.mapError(error))),
        {
          enableHighAccuracy: false,
          timeout: ACQUIRE_TIMEOUT_MS,
          maximumAge: MAX_CACHE_AGE_MS,
        },
      );
    });
  }

  /** Resolve the platform `Geolocation`, or `null` where the API is unavailable (SSR / old env). */
  private geolocation(): Geolocation | null {
    try {
      return globalThis.navigator?.geolocation ?? null;
    } catch {
      return null;
    }
  }

  /** Map a `GeolocationPositionError` code to our typed reason (unknown codes → unavailable). */
  private mapError(error: GeolocationPositionError): GeolocationFailureReason {
    switch (error.code) {
      case error.PERMISSION_DENIED:
        return 'permission-denied';
      case error.TIMEOUT:
        return 'timeout';
      case error.POSITION_UNAVAILABLE:
      default:
        return 'position-unavailable';
    }
  }

  /** Build a handled failure for a reason, attaching its stable i18n message key. */
  private failure(reason: GeolocationFailureReason): GeolocationFailure {
    return { ok: false, reason, messageKey: GEO_MESSAGE_KEYS[reason] };
  }
}
