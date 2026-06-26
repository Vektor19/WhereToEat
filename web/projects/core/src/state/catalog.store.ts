/**
 * Catalog store — caches the rarely-changing two-level taxonomy (categories + dishes-by-category)
 * so the deterministic selection flow (invariant #1 & #2) reads it instantly and the app does not
 * re-fetch the same list on every visit (design's performance note: "cache the rarely-changing
 * taxonomy").
 *
 * It is fed by the data-access operation ports (Step 5) through their injection tokens, drives the
 * async lifecycle through the shared {@link RequestState} primitive (Step 7) so spinner/error/empty
 * are consistent, and exposes only readable signals plus explicit `loadCategories` /
 * `loadDishes(categoryId)` mutators. Caching rule: once a list has loaded, a repeat load is a no-op
 * (no duplicate fetch) unless `force` is passed — so a second read of the categories, or of a
 * category's dishes, never hits the network again.
 *
 * View-independent (no DOM, no component) so its shape mirrors 1:1 onto an RN container.
 *
 * Like every store in the app it extends {@link SignalStore} (Step 9 preamble — the base is applied
 * consistently): the categories {@link RequestState} and the per-category dishes map live as members
 * of the base-managed immutable state object, so the store has the same 1:1 RN-blueprint shape as the
 * others and there are no public setters on a raw signal.
 */
import { Injectable, computed, inject, type Signal } from '@angular/core';

import type { CategoryDto, DishDto } from '../domain';
import type { ApiError } from '../domain/api-error';
import {
  GET_CATEGORIES,
  GET_DISHES_BY_CATEGORY,
  type GetCategoriesOperation,
  type GetDishesByCategoryOperation,
} from '../data-access';
import { LoadingService, type RequestState } from '../ui-state/loading.service';
import { SignalStore } from './signal-store.base';

/**
 * The immutable catalog state the store owns. Both members are stable references created once in the
 * constructor — the categories {@link RequestState} and the per-category dishes map (whose entries
 * are added lazily). Their reactivity comes from the {@link RequestState} signals inside them; the
 * state object itself only re-emits when the map grows (a new category is first requested), which is
 * what lets the per-category readable signals track a category created after the store.
 */
interface CatalogState {
  /** The categories request — one shared lifecycle (the taxonomy's top level is loaded once). */
  readonly categories: RequestState<readonly CategoryDto[]>;
  /**
   * One {@link RequestState} per category id, lazily created on first request. Each holds that
   * category's dishes and its own loading/error sub-state, so the UI can show a per-category spinner
   * while another category's dishes are already cached.
   */
  readonly dishesByCategory: ReadonlyMap<string, RequestState<readonly DishDto[]>>;
}

@Injectable({ providedIn: 'root' })
export class CatalogStore extends SignalStore<CatalogState> {
  private readonly getCategories: GetCategoriesOperation;
  private readonly getDishesByCategory: GetDishesByCategoryOperation;
  private readonly loading: LoadingService;
  /** The categories request lifecycle (also held in state — a stable reference). */
  private readonly categoriesState: RequestState<readonly CategoryDto[]>;
  /**
   * A single idle, never-mutated {@link RequestState} returned by the dishes **read** accessors
   * when a category has not been loaded yet. Reading is side-effect-free: it never creates a
   * per-category entry (which would be a signal write — NG0600 inside a `computed`). Only
   * `loadDishes` creates the real entry; until then a category reads as `idle` (no dishes, not
   * loading, no error) off this stable sentinel.
   */
  private readonly emptyDishesState: RequestState<readonly DishDto[]>;

  constructor() {
    const getCategories = inject(GET_CATEGORIES);
    const getDishesByCategory = inject(GET_DISHES_BY_CATEGORY);
    const loading = inject(LoadingService);
    const categoriesState = loading.create<readonly CategoryDto[]>();
    super({ categories: categoriesState, dishesByCategory: new Map() });
    this.getCategories = getCategories;
    this.getDishesByCategory = getDishesByCategory;
    this.loading = loading;
    this.categoriesState = categoriesState;
    this.emptyDishesState = loading.create<readonly DishDto[]>();
  }

  // ── Categories (top level) ──────────────────────────────────────────────────

  /** The loaded categories, or `undefined` until the first successful load. */
  readonly categories: Signal<readonly CategoryDto[] | undefined> = this.select((s) =>
    s.categories.data(),
  );
  /** True while the categories request is in flight. */
  readonly categoriesLoading: Signal<boolean> = this.select((s) => s.categories.loading());
  /** The typed error from the last failed categories load, or `undefined`. */
  readonly categoriesError: Signal<ApiError | undefined> = this.select((s) => s.categories.error());
  /** True once categories have loaded but the list is empty. */
  readonly categoriesEmpty: Signal<boolean> = this.select((s) => s.categories.empty());

  /** True once the categories have been loaded successfully at least once (cache is warm). */
  readonly categoriesLoaded: Signal<boolean> = this.select((s) => s.categories.loaded());

  /**
   * Load the taxonomy's top level. **Cached:** if categories have already loaded successfully (or a
   * request is in flight) this is a no-op, so a repeat read never re-fetches. Pass `force` to bypass
   * the cache (e.g. an explicit refresh).
   */
  loadCategories(force = false): void {
    if (!force && (this.categoriesState.loaded() || this.categoriesState.loading())) {
      return;
    }
    this.categoriesState.run(this.getCategories.execute());
  }

  // ── Dishes (lower level, per category) ──────────────────────────────────────

  /**
   * The cached dishes for a category, or `undefined` if that category has not been loaded.
   * **Read-only:** reading never creates a per-category entry (it falls back to the idle
   * {@link emptyDishesState} sentinel), so this is safe to call from inside a `computed`. It is
   * reactive — once `loadDishes` creates the real entry, this signal tracks it.
   */
  dishes(categoryId: string): Signal<readonly DishDto[] | undefined> {
    return computed(() => this.readDishesState(categoryId).data());
  }

  /** True while the given category's dishes request is in flight. Read-only (no entry creation). */
  dishesLoading(categoryId: string): Signal<boolean> {
    return computed(() => this.readDishesState(categoryId).loading());
  }

  /** The typed error from the given category's last failed dishes load, or `undefined`. Read-only. */
  dishesError(categoryId: string): Signal<ApiError | undefined> {
    return computed(() => this.readDishesState(categoryId).error());
  }

  /** True once the given category's dishes have loaded but the list is empty. Read-only. */
  dishesEmpty(categoryId: string): Signal<boolean> {
    return computed(() => this.readDishesState(categoryId).empty());
  }

  /**
   * Load the dishes within a category. **Cached per category:** if that category's dishes have
   * already loaded successfully (or are in flight) this is a no-op — re-opening a category reuses the
   * cached list rather than re-fetching. Pass `force` to bypass the cache. This is the only path
   * that creates a per-category entry (the read accessors never do).
   */
  loadDishes(categoryId: string, force = false): void {
    const state = this.ensureDishesState(categoryId);
    if (!force && (state.loaded() || state.loading())) {
      return;
    }
    state.run(this.getDishesByCategory.execute(categoryId));
  }

  /**
   * Look up a category's dishes {@link RequestState} **without** creating it — returns the idle
   * {@link emptyDishesState} sentinel when the entry does not exist yet. Side-effect-free, so the
   * read accessors that build their `computed`s on it never write a signal (NG0600-safe).
   */
  private readDishesState(categoryId: string): RequestState<readonly DishDto[]> {
    return this.state().dishesByCategory.get(categoryId) ?? this.emptyDishesState;
  }

  /**
   * Get (creating on first use) the per-category dishes {@link RequestState}. A newly-created entry
   * is committed back into the base-managed state through `patchState` (a fresh map) so the state
   * object stays the single source of truth and no raw signal is mutated outside the base. Called
   * only from `loadDishes`, never from a read accessor.
   */
  private ensureDishesState(categoryId: string): RequestState<readonly DishDto[]> {
    const existing = this.state().dishesByCategory.get(categoryId);
    if (existing !== undefined) {
      return existing;
    }
    const created = this.loading.create<readonly DishDto[]>();
    this.patchState((s) => ({
      dishesByCategory: new Map(s.dishesByCategory).set(categoryId, created),
    }));
    return created;
  }

  /** A category looked up by id from the loaded categories, or `undefined`. */
  categoryById(categoryId: string): Signal<CategoryDto | undefined> {
    return computed(() => this.categories()?.find((c) => c.id === categoryId));
  }

  /**
   * A dish's canonical name looked up by id across **already-loaded** dishes, or `undefined` when no
   * loaded category contains it. **Read-only and fetch-free** — it scans the cached per-category
   * dishes only (never triggers a load), so it is safe inside a `computed`. The restaurant-details
   * menu (Step 14) uses it to label a menu item (whose DTO carries only `dishId`); when the taxonomy
   * for that dish has not been browsed yet it returns `undefined` and the view falls back to a neutral
   * label rather than forcing a full taxonomy fetch on the details page.
   */
  dishNameById(dishId: string): Signal<string | undefined> {
    return computed(() => {
      for (const state of this.state().dishesByCategory.values()) {
        const dish = state.data()?.find((d) => d.id === dishId);
        if (dish !== undefined) {
          return dish.canonicalName;
        }
      }
      return undefined;
    });
  }
}
