/**
 * Contract key value unions — the pinned string keys the recommendation engine resolves to
 * pluggable strategies (invariant #4). These mirror the backend's accepted `match` / `sort` /
 * filter `key` values exactly, so the request builder (Step 8) and the query controls (Step 11)
 * cannot emit a key the engine would reject. New modes are added behind a new key value here
 * without changing the request/response contract shape.
 */

/**
 * The match predicate (CLAUDE.md §6, Step 1): `or` = at least one selected item present, `and` =
 * all selected items present (a "combo"). Future modes ("at least K of N", …) become new key
 * values without restructuring.
 */
export type MatchMode = 'or' | 'and';

/** The runtime list of {@link MatchMode} values — asserted against the contract by the unit tests. */
export const MATCH_MODES: readonly MatchMode[] = ['or', 'and'] as const;

/**
 * The sort mode: the three explicit single-field sorts (`price` / `distance` / `rating`) where
 * the chosen field dominates and coverage is only a tie-breaker (invariant #5), plus the two
 * composite modes (`price-quality` / `best`) where a composed score is computed.
 */
export type SortMode = 'price' | 'distance' | 'rating' | 'price-quality' | 'best';

/** The runtime list of {@link SortMode} values — asserted against the contract by the unit tests. */
export const SORT_MODES: readonly SortMode[] = [
  'price',
  'distance',
  'rating',
  'price-quality',
  'best',
] as const;

/**
 * The composable filter keys available today (`price` max, `rating` min). The filter list is
 * data-driven so a new key (open-now, vegan, delivery, …) is additive (invariant #4).
 */
export type FilterKey = 'price' | 'rating';

/** The runtime list of {@link FilterKey} values — asserted against the contract by the unit tests. */
export const FILTER_KEYS: readonly FilterKey[] = ['price', 'rating'] as const;
