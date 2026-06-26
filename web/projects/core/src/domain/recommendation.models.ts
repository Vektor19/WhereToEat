/**
 * Recommendation request/result models — the TypeScript mirror of
 * `WhereToEat.Contracts.Recommendation` (`RecommendationRequest`, `SelectedItem`,
 * `FilterSelection`, `UserGeo`, `RecommendationResultDto`, `RecommendedRestaurantDto`).
 *
 * Field names match the camelCased wire form. This is the single source of truth the request
 * builder (Step 8) serialises to and the results store (Step 9) reads — so the recommend body
 * matches the backend wire shape field-for-field. Search stays deterministic (invariant #1):
 * `items` are explicit category/dish selections, never free text.
 */

import type { FilterKey, MatchMode, SortMode } from './contract-keys';

/**
 * One selected catalog item — **exactly one** of `categoryId` / `dishId` is present (the
 * two-level taxonomy, invariant #2). Modelled as a discriminated union so the type system
 * forbids the invalid states the backend's private `SelectedItem` constructor forbids: neither
 * id (an empty selection) and both ids (an ambiguous selection) are unrepresentable. Consumers
 * narrow on which key is present (use {@link isCategorySelection} / {@link isDishSelection}).
 */
export type SelectedItem = CategorySelection | DishSelection;

/** A selection of a whole category — the dish id is absent (invariant #2). */
export interface CategorySelection {
  readonly categoryId: string;
  readonly dishId?: never;
}

/** A selection of a specific dish — the category id is absent (invariant #2). */
export interface DishSelection {
  readonly dishId: string;
  readonly categoryId?: never;
}

/** Type guard: the selection is a category (and not a dish). */
export function isCategorySelection(item: SelectedItem): item is CategorySelection {
  return item.categoryId !== undefined;
}

/** Type guard: the selection is a dish (and not a category). */
export function isDishSelection(item: SelectedItem): item is DishSelection {
  return item.dishId !== undefined;
}

/** A composable filter selection (price, rating, future open-now/vegan/… by key). */
export interface FilterSelection {
  readonly key: FilterKey;
  readonly value?: string | null;
}

/**
 * The user's approximate location for distance ranking. Optional: distance is on by default
 * but the user may omit/disable it to range further for price or quality (invariant #11 —
 * opt-in approximate geo).
 */
export interface UserGeo {
  readonly latitude: number;
  readonly longitude: number;
}

/**
 * The public recommendation request shape from CLAUDE.md §6:
 * `{ items, match, sort, filters, userGeo }`. `match`/`sort`/filter keys are the pinned contract
 * unions (invariant #4 — pluggable engine keys), so this contract does not change when new modes
 * are added behind a known key.
 */
export interface RecommendationRequest {
  readonly items: readonly SelectedItem[];
  readonly match: MatchMode;
  readonly sort: SortMode;
  readonly filters: readonly FilterSelection[];
  readonly userGeo?: UserGeo;
}

/**
 * One ranked restaurant in a recommendation result. Carries the representative figures the
 * engine computed (basket price, our smoothed rating, distance, coverage) so the client renders
 * them without a second call. Ratings/coords are always our own — never Google (invariants
 * #6/#7). Optional fields are null/absent when the engine could not compute them (e.g.
 * `distanceKm` when no `userGeo` was supplied).
 */
export interface RecommendedRestaurantDto {
  readonly restaurantId: string;
  readonly name: string;
  readonly basketPriceAmount?: number | null;
  readonly basketPriceCurrency?: string | null;
  readonly smoothedRating?: number | null;
  readonly ratingCount: number;
  readonly distanceKm?: number | null;
  readonly coverage: number;
}

/**
 * The public recommendation response: an ordered list of ranked restaurants plus the echoed
 * match/sort keys that produced it. **Order is significant** — it is the final ranked, filtered
 * list and the client never re-sorts it (explicit sort dominates; coverage is only a tie-breaker
 * — invariant #5).
 */
export interface RecommendationResultDto {
  readonly match: MatchMode;
  readonly sort: SortMode;
  readonly restaurants: readonly RecommendedRestaurantDto[];
}
