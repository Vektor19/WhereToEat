/**
 * Selection store — the user's current deterministic query: the selected catalog items
 * (categories and/or dishes, each exactly one of the two taxonomy levels — invariant #2), the
 * chosen match mode (Or/And), the sort mode, the composable filters, and the optional opt-in
 * `userGeo` (invariant #11). This is the single state the {@link RecommendRequestBuilder} (Step 8)
 * consumes to produce the `recommend` wire body, so the store is the seam between the UI controls
 * (Steps 10–11, 15) and the request.
 *
 * It exposes readable signals plus explicit mutators (`addItem` / `removeItem` / `clear` /
 * `setMatch` / `setSort` / `setFilter` / `removeFilter` / `setGeo` / `clearGeo`). Selection rules
 * are enforced through the {@link RecommendValidationService}: items are de-duplicated by id and an
 * item that is not exactly one valid id is rejected; an out-of-range `userGeo` is rejected. The
 * store builds on the {@link SignalStore} base so all mutation goes through immutable patches and
 * there are no public setters on a raw signal.
 *
 * Defaults mirror the product's sensible starting query: match `or` (at least one — §6) and sort
 * `best` (the composite default); both are overridable. View-independent (no DOM) so it mirrors 1:1
 * onto an RN container.
 */
import { Injectable, computed, inject, type Signal } from '@angular/core';

import {
  isCategorySelection,
  isDishSelection,
  type FilterKey,
  type FilterSelection,
  type MatchMode,
  type SelectedItem,
  type SortMode,
  type UserGeo,
} from '../domain';
import { RecommendRequestBuilder, type BuildResult } from '../recommend/recommend-request.builder';
import { RecommendValidationService } from '../validation/recommend-validation.service';
import { SignalStore } from './signal-store.base';

/** The default match mode — `or` ("at least one selected item present", §6). */
const DEFAULT_MATCH: MatchMode = 'or';
/** The default sort mode — `best` (the composite "best overall" mode, §6). */
const DEFAULT_SORT: SortMode = 'best';

/** The immutable selection state the store owns. */
export interface SelectionState {
  readonly items: readonly SelectedItem[];
  readonly match: MatchMode;
  readonly sort: SortMode;
  readonly filters: readonly FilterSelection[];
  readonly userGeo?: UserGeo;
}

const INITIAL_STATE: SelectionState = {
  items: [],
  match: DEFAULT_MATCH,
  sort: DEFAULT_SORT,
  filters: [],
};

/** The id an item carries, regardless of which taxonomy level it is (for de-duplication). */
function itemId(item: SelectedItem): string | undefined {
  return isDishSelection(item) ? item.dishId : item.categoryId;
}

/** True when two selections refer to the same catalog entity at the same taxonomy level. */
function sameItem(a: SelectedItem, b: SelectedItem): boolean {
  return (
    isCategorySelection(a) === isCategorySelection(b) &&
    isDishSelection(a) === isDishSelection(b) &&
    itemId(a) === itemId(b)
  );
}

@Injectable({ providedIn: 'root' })
export class SelectionStore extends SignalStore<SelectionState> {
  private readonly validation = inject(RecommendValidationService);
  private readonly builder = inject(RecommendRequestBuilder);

  constructor() {
    super(INITIAL_STATE);
  }

  // ── Readable signals ────────────────────────────────────────────────────────

  /** The current selected items (categories and/or dishes). */
  readonly items: Signal<readonly SelectedItem[]> = this.select((s) => s.items);
  /** The chosen match mode (Or/And). */
  readonly match: Signal<MatchMode> = this.select((s) => s.match);
  /** The chosen sort mode. */
  readonly sort: Signal<SortMode> = this.select((s) => s.sort);
  /** The applied composable filters. */
  readonly filters: Signal<readonly FilterSelection[]> = this.select((s) => s.filters);
  /** The opt-in approximate `userGeo`, or `undefined` when distance ranking is off. */
  readonly userGeo: Signal<UserGeo | undefined> = this.select((s) => s.userGeo);

  /** True when there is at least one selected item (i.e. a request could be built). */
  readonly hasSelection: Signal<boolean> = computed(() => this.items().length > 0);
  /** The count of selected items. */
  readonly count: Signal<number> = computed(() => this.items().length);

  // ── Selection mutators ──────────────────────────────────────────────────────

  /**
   * Add a category or dish to the selection. Rejected (no-op) when the item is not exactly one
   * valid id (validated like the backend — invariant #2) or is already selected (de-duplicated by
   * id + level), so the selection never carries a duplicate or an ambiguous item.
   */
  addItem(item: SelectedItem): void {
    if (!this.validation.validateSelection([item]).valid) {
      return;
    }
    this.patchState((s) =>
      s.items.some((existing) => sameItem(existing, item)) ? {} : { items: [...s.items, item] },
    );
  }

  /** Remove a selected item (by id + level). A no-op when it is not in the selection. */
  removeItem(item: SelectedItem): void {
    this.patchState((s) => ({ items: s.items.filter((existing) => !sameItem(existing, item)) }));
  }

  /** True when the given item is currently selected. */
  isSelected(item: SelectedItem): boolean {
    return this.items().some((existing) => sameItem(existing, item));
  }

  /** Clear the selected items only (match/sort/filters/geo are left untouched). */
  clearItems(): void {
    this.patchState({ items: [] });
  }

  /** Reset the whole selection (items, match, sort, filters, geo) back to the defaults. */
  clear(): void {
    this.setState(INITIAL_STATE);
  }

  // ── Mode / filter / geo mutators ────────────────────────────────────────────

  /** Set the match mode. Rejected (no-op) when not a known contract key. */
  setMatch(match: MatchMode): void {
    if (!this.validation.isKnownMatch(match)) {
      return;
    }
    this.patchState({ match });
  }

  /** Set the sort mode. Rejected (no-op) when not a known contract key. */
  setSort(sort: SortMode): void {
    if (!this.validation.isKnownSort(sort)) {
      return;
    }
    this.patchState({ sort });
  }

  /**
   * Apply (or replace) a composable filter by its key — one filter per key, so re-applying a key
   * updates its value rather than duplicating it. Rejected (no-op) when the key is not a known
   * contract key (invariant #4 — keys are pinned).
   */
  setFilter(key: FilterKey, value?: string | null): void {
    if (!this.validation.isKnownFilterKey(key)) {
      return;
    }
    this.patchState((s) => ({
      filters: [...s.filters.filter((f) => f.key !== key), { key, value: value ?? null }],
    }));
  }

  /** Remove a composable filter by key. A no-op when the key is not applied. */
  removeFilter(key: FilterKey): void {
    this.patchState((s) => ({ filters: s.filters.filter((f) => f.key !== key) }));
  }

  /** Clear all composable filters. */
  clearFilters(): void {
    this.patchState({ filters: [] });
  }

  /**
   * Opt in to distance ranking by setting the approximate `userGeo` (invariant #11). Rejected
   * (no-op) when the coordinates are out of range (validated like the backend), so an invalid geo
   * never reaches the request.
   */
  setGeo(geo: UserGeo): void {
    if (!this.validation.validateUserGeo(geo).valid) {
      return;
    }
    this.patchState({ userGeo: geo });
  }

  /**
   * Opt out of distance ranking by dropping `userGeo` entirely (so the wire body omits it —
   * invariant #11). Uses `setState` to rebuild the state without the `userGeo` key, because a
   * partial patch would merge over the old value rather than delete the key.
   */
  clearGeo(): void {
    const { userGeo: _omitted, ...rest } = this.state();
    void _omitted;
    this.setState(rest);
  }

  // ── Request building ────────────────────────────────────────────────────────

  /**
   * Build (and validate) the {@link RecommendationRequest} from the current selection via the
   * Step 8 {@link RecommendRequestBuilder}. Returns the typed {@link BuildResult} — a valid request
   * or the list of validation problems — so the submit flow (Step 11) can fail fast before the
   * round-trip.
   */
  buildRequest(): BuildResult {
    const state = this.state();
    return this.builder.tryBuild({
      items: state.items,
      match: state.match,
      sort: state.sort,
      filters: state.filters,
      ...(state.userGeo === undefined ? {} : { userGeo: state.userGeo }),
    });
  }
}
