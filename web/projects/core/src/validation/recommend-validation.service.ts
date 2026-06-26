/**
 * Recommendation-request validation — mirrors the backend's pre-pipeline validation so the UI
 * fails fast before a round-trip (Step 8).
 *
 * The backend's `RecommendRequestBody.TryToContract` rejects, with a descriptive 400, any of:
 *   - an item with neither id or both ids (`Recommend.InvalidItem`);
 *   - an out-of-range `userGeo` (`Recommend.InvalidUserGeo` — `GeoPoint` bounds
 *     latitude ∈ [-90, 90], longitude ∈ [-180, 180], finite);
 * and the engine further requires a known `match` / `sort` / filter `key`. This service reproduces
 * those rules **client-side** and returns a typed {@link ValidationResult} — it never throws for
 * user-input errors (throwing is for programming errors only) so the UI can surface each problem.
 *
 * Framework-light and pure: no `HttpClient`, no view coupling — an RN rewrite mirrors it 1:1.
 */
import { Injectable } from '@angular/core';

import {
  FILTER_KEYS,
  MATCH_MODES,
  SORT_MODES,
  isCategorySelection,
  isDishSelection,
} from '../domain';
import type {
  FilterKey,
  FilterSelection,
  MatchMode,
  RecommendationRequest,
  SelectedItem,
  SortMode,
  UserGeo,
} from '../domain';

/** Inclusive latitude bounds in decimal degrees (mirrors `GeoPoint.Min/MaxLatitude`). */
export const MIN_LATITUDE = -90;
export const MAX_LATITUDE = 90;

/** Inclusive longitude bounds in decimal degrees (mirrors `GeoPoint.Min/MaxLongitude`). */
export const MIN_LONGITUDE = -180;
export const MAX_LONGITUDE = 180;

/**
 * The stable machine codes for validation failures. They mirror the backend's `error` codes where
 * one exists (`Recommend.InvalidItem`, `Recommend.InvalidUserGeo`) and add client-only codes for the
 * checks the engine performs after binding (empty selection, unknown keys) so the UI never sends a
 * request the backend would reject.
 */
export type ValidationCode =
  | 'Recommend.EmptySelection'
  | 'Recommend.InvalidItem'
  | 'Recommend.InvalidUserGeo'
  | 'Recommend.UnknownMatch'
  | 'Recommend.UnknownSort'
  | 'Recommend.UnknownFilterKey';

/** A single validation problem: a stable {@link ValidationCode} plus a human-readable message. */
export interface ValidationError {
  readonly code: ValidationCode;
  readonly message: string;
}

/**
 * The outcome of validating user input. `valid` narrows the union: a valid result carries an empty
 * `errors` list, an invalid one carries at least one {@link ValidationError}. This is returned, never
 * thrown, for user-input problems.
 */
export type ValidationResult =
  | { readonly valid: true; readonly errors: readonly [] }
  | { readonly valid: false; readonly errors: readonly ValidationError[] };

const VALID: ValidationResult = { valid: true, errors: [] };

function invalid(errors: readonly ValidationError[]): ValidationResult {
  return { valid: false, errors };
}

@Injectable({ providedIn: 'root' })
export class RecommendValidationService {
  /**
   * Validate a fully-assembled {@link RecommendationRequest} (the shape the request builder
   * produces). Aggregates every problem found — selection emptiness, per-item id ambiguity, match/
   * sort/filter key validity, and `userGeo` range — so the UI can show them all at once.
   */
  validateRequest(request: RecommendationRequest): ValidationResult {
    const errors: ValidationError[] = [
      ...this.collectSelectionErrors(request.items),
      ...this.collectMatchErrors(request.match),
      ...this.collectSortErrors(request.sort),
      ...this.collectFilterErrors(request.filters),
      ...this.collectGeoErrors(request.userGeo),
    ];

    return errors.length === 0 ? VALID : invalid(errors);
  }

  /**
   * Validate just the selection list (non-empty + each item exactly one of category/dish). Exposed
   * for the selection store (Step 9), which validates the selection independently of the chosen
   * match/sort/filters.
   */
  validateSelection(items: readonly SelectedItem[]): ValidationResult {
    const errors = this.collectSelectionErrors(items);
    return errors.length === 0 ? VALID : invalid(errors);
  }

  /**
   * Validate a `userGeo` candidate (latitude/longitude finite + in range). Exposed for the
   * geolocation flow (Step 15), which validates acquired coordinates before feeding `userGeo`.
   */
  validateUserGeo(geo: UserGeo): ValidationResult {
    const errors = this.collectGeoErrors(geo);
    return errors.length === 0 ? VALID : invalid(errors);
  }

  /** True when `value` is one of the pinned {@link MatchMode} contract keys. */
  isKnownMatch(value: string): value is MatchMode {
    return (MATCH_MODES as readonly string[]).includes(value);
  }

  /** True when `value` is one of the pinned {@link SortMode} contract keys. */
  isKnownSort(value: string): value is SortMode {
    return (SORT_MODES as readonly string[]).includes(value);
  }

  /** True when `value` is one of the pinned {@link FilterKey} contract keys. */
  isKnownFilterKey(value: string): value is FilterKey {
    return (FILTER_KEYS as readonly string[]).includes(value);
  }

  private collectSelectionErrors(items: readonly SelectedItem[]): ValidationError[] {
    const errors: ValidationError[] = [];

    if (items.length === 0) {
      errors.push({
        code: 'Recommend.EmptySelection',
        message: 'Select at least one category or dish before requesting recommendations.',
      });
    }

    for (const item of items) {
      // The discriminated union forbids both-ids/neither-ids at compile time, but inputs can come
      // from untyped sources (forms, JSON) — so we re-check at runtime exactly like the backend.
      const hasCategory = isCategorySelection(item) && this.isNonEmptyId(item.categoryId);
      const hasDish = isDishSelection(item) && this.isNonEmptyId(item.dishId);

      if (hasCategory === hasDish) {
        errors.push({
          code: 'Recommend.InvalidItem',
          message: 'Each selected item must specify exactly one of categoryId or dishId.',
        });
      }
    }

    return errors;
  }

  private collectMatchErrors(match: string): ValidationError[] {
    return this.isKnownMatch(match)
      ? []
      : [
          {
            code: 'Recommend.UnknownMatch',
            message: `Unknown match mode '${match}'. Expected one of: ${MATCH_MODES.join(', ')}.`,
          },
        ];
  }

  private collectSortErrors(sort: string): ValidationError[] {
    return this.isKnownSort(sort)
      ? []
      : [
          {
            code: 'Recommend.UnknownSort',
            message: `Unknown sort mode '${sort}'. Expected one of: ${SORT_MODES.join(', ')}.`,
          },
        ];
  }

  private collectFilterErrors(filters: readonly FilterSelection[]): ValidationError[] {
    const errors: ValidationError[] = [];
    for (const filter of filters) {
      if (!this.isKnownFilterKey(filter.key)) {
        errors.push({
          code: 'Recommend.UnknownFilterKey',
          message: `Unknown filter key '${filter.key}'. Expected one of: ${FILTER_KEYS.join(', ')}.`,
        });
      }
    }
    return errors;
  }

  private collectGeoErrors(geo: UserGeo | undefined): ValidationError[] {
    if (geo === undefined) {
      // Omitting userGeo is valid — distance ranking is opt-in (invariant #11).
      return [];
    }

    const latInRange =
      Number.isFinite(geo.latitude) && geo.latitude >= MIN_LATITUDE && geo.latitude <= MAX_LATITUDE;
    const lngInRange =
      Number.isFinite(geo.longitude) &&
      geo.longitude >= MIN_LONGITUDE &&
      geo.longitude <= MAX_LONGITUDE;

    if (latInRange && lngInRange) {
      return [];
    }

    return [
      {
        code: 'Recommend.InvalidUserGeo',
        message: 'userGeo latitude must be within [-90, 90] and longitude within [-180, 180].',
      },
    ];
  }

  private isNonEmptyId(id: string | undefined): boolean {
    return typeof id === 'string' && id.trim().length > 0;
  }
}
