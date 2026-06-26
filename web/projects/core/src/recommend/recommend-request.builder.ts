/**
 * Recommendation request builder — turns the user's selection (categories/dishes) + chosen match +
 * sort + filters (+ optional `userGeo`) into a well-formed {@link RecommendationRequest} (Step 8).
 *
 * This is the single place that assembles the request the data-access `recommend` operation (Step 5)
 * serialises to the exact wire body (`items: [{categoryId|dishId}]`, `match`, `sort`,
 * `filters: [{key, value}]`, optional `userGeo: {latitude, longitude}`). It is **pure and
 * deterministic** — selections in, request out, no NLP (invariant #1), two-level taxonomy
 * (invariant #2). It enforces "exactly one of categoryId/dishId" per item and (optionally) a
 * non-empty selection by validating up-front via the {@link RecommendValidationService}, so the UI
 * fails fast before a round-trip rather than letting the backend 400.
 *
 * Framework-light: an injectable service with no view coupling, mirrored 1:1 by the RN rewrite.
 */
import { Injectable, inject } from '@angular/core';

import type {
  FilterSelection,
  MatchMode,
  RecommendationRequest,
  SelectedItem,
  SortMode,
  UserGeo,
} from '../domain';
import { RecommendValidationService } from '../validation/recommend-validation.service';
import type { ValidationError, ValidationResult } from '../validation/recommend-validation.service';

/**
 * The inputs that make up a recommendation request — the selection plus the chosen match/sort, the
 * (optional) composable filters, and the (optional, opt-in) `userGeo`. Filters/geo default to
 * absent so callers pass only what the user chose.
 */
export interface RecommendRequestInput {
  readonly items: readonly SelectedItem[];
  readonly match: MatchMode;
  readonly sort: SortMode;
  readonly filters?: readonly FilterSelection[];
  readonly userGeo?: UserGeo;
}

/**
 * The result of {@link RecommendRequestBuilder.tryBuild}: either a valid built request or the list
 * of validation problems. Returned (never thrown) for user-input errors so the caller can surface
 * each problem before any round-trip.
 */
export type BuildResult =
  | { readonly valid: true; readonly request: RecommendationRequest }
  | { readonly valid: false; readonly errors: readonly ValidationError[] };

@Injectable({ providedIn: 'root' })
export class RecommendRequestBuilder {
  private readonly validation = inject(RecommendValidationService);

  /**
   * Assemble the domain {@link RecommendationRequest} from the input — this **does not** validate;
   * it only normalises the shape. The output is normalised: filters default to an empty list and
   * `userGeo` is included only when supplied (so the wire body omits it entirely when geo is off —
   * invariant #11). Use {@link tryBuild} as the safe entry point at the UI boundary — it validates
   * the assembled request and returns typed errors instead of letting an invalid selection reach the
   * network.
   */
  build(input: RecommendRequestInput): RecommendationRequest {
    const base = {
      items: [...input.items],
      match: input.match,
      sort: input.sort,
      filters: [...(input.filters ?? [])],
    };

    return input.userGeo === undefined ? base : { ...base, userGeo: input.userGeo };
  }

  /**
   * Validate the input and build the request in one step. Mirrors the backend's pre-pipeline
   * validation (exactly-one-of-id per item, non-empty selection, known match/sort/filter keys,
   * `userGeo` range) so an invalid selection never reaches the network — the UI shows the typed
   * errors instead.
   */
  tryBuild(input: RecommendRequestInput): BuildResult {
    const request = this.build(input);
    const result: ValidationResult = this.validation.validateRequest(request);

    return result.valid ? { valid: true, request } : { valid: false, errors: result.errors };
  }
}
