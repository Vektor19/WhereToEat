import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatTabsModule } from '@angular/material/tabs';
import { TranslatePipe } from '@ngx-translate/core';
import { Subject, forkJoin, of, type Observable } from 'rxjs';
import { catchError, debounceTime, distinctUntilChanged, map, switchMap } from 'rxjs/operators';

import {
  AnalyticsEmitterService,
  AnalyticsEventBuilders,
  CatalogStore,
  SelectionStore,
  SEARCH_CATEGORIES,
  SEARCH_DISHES,
  isCategorySelection,
  isDishSelection,
  type ApiError,
  type CategoryDto,
  type DishDto,
  type SelectedItem,
} from 'core';

import { QueryControlsComponent } from '../query-controls/query-controls.component';
import { ResultsListComponent } from '../results/results-list.component';
import { CategoryBrowserComponent } from './category-browser.component';
import { PrefixSearchComponent, type PrefixSearchResult } from './prefix-search.component';
import { SelectionSummaryComponent, type SelectionChip } from './selection-summary.component';

/**
 * Debounce window for the prefix-search input (the design's "debounce prefix-search input"
 * performance note). Keeps the deterministic anchored lookups (invariant #1) snappy without a
 * request per keystroke.
 */
const SEARCH_DEBOUNCE_MS = 250;
/** Cap on prefix-search results (the data-access `limit`). Anchored prefix, never free-text NLP. */
const SEARCH_LIMIT = 10;
/** Below this prefix length the typeahead does not query (avoids a full-catalog scan on one letter). */
const MIN_PREFIX_LENGTH = 2;

/** A single in-flight prefix search's snapshot the template binds to (results + loading + error). */
interface SearchState {
  readonly queried: boolean;
  readonly loading: boolean;
  readonly error?: ApiError;
  readonly categories: readonly CategoryDto[];
  readonly dishes: readonly DishDto[];
}

const EMPTY_SEARCH: SearchState = { queried: false, loading: false, categories: [], dishes: [] };

/**
 * Selection-builder container (smart) — the consumer surface's first real feature and the entry
 * point of the deterministic selection flow (invariant #1 & #2).
 *
 * It wires the cached {@link CatalogStore} (two-level taxonomy browse) and the {@link SelectionStore}
 * (the user's selection), and owns the **anchored prefix-search** stream: the {@link PrefixSearchComponent}
 * emits raw input, which this container debounces and runs through the `SEARCH_CATEGORIES` /
 * `SEARCH_DISHES` data-access ports — **no free text ever reaches `recommend`**. Picking a result or a
 * browsed category/dish adds exactly one {@link SelectedItem} (a category or a dish) to the store.
 *
 * The presentational children are pure inputs/outputs; this container is the only place that touches
 * stores/data-access/analytics, so the smart/dumb seam lines up with the business-logic-vs-view
 * boundary the RN rewrite mirrors. On "search" it emits the `search` analytics event through the
 * single batching emitter (best-effort, non-blocking).
 */
@Component({
  selector: 'app-selection-builder',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatButtonModule,
    MatCardModule,
    MatIconModule,
    MatTabsModule,
    CategoryBrowserComponent,
    PrefixSearchComponent,
    SelectionSummaryComponent,
    QueryControlsComponent,
    ResultsListComponent,
    TranslatePipe,
  ],
  template: `
    <div class="dp-builder">
      <app-selection-summary [chips]="chips()" (remove)="onRemove($event)" (clear)="onClear()" />

      <mat-card class="dp-builder__panel" appearance="outlined">
        <mat-tab-group dynamicHeight>
          <mat-tab [label]="'consumer.browse.tabLabel' | translate">
            <div class="dp-builder__tab">
              <app-category-browser
                [categories]="categories()"
                [categoriesLoading]="categoriesLoading()"
                [categoriesError]="categoriesError()"
                [categoriesEmpty]="categoriesEmpty()"
                [openCategory]="openCategory()"
                [dishes]="openDishes()"
                [dishesLoading]="openDishesLoading()"
                [dishesError]="openDishesError()"
                [dishesEmpty]="openDishesEmpty()"
                [selectedCategoryIds]="selectedCategoryIds()"
                [selectedDishIds]="selectedDishIds()"
                (openCategoryRequest)="onOpenCategory($event)"
                (closeCategory)="onCloseCategory()"
                (addCategory)="onAddCategory($event)"
                (addDish)="onAddDish($event)"
                (reloadCategories)="reloadCategories()"
                (reloadDishes)="reloadDishes($event)"
              />
            </div>
          </mat-tab>

          <mat-tab [label]="'consumer.browse.searchTabLabel' | translate">
            <div class="dp-builder__tab">
              <app-prefix-search
                [results]="searchResults()"
                [loading]="search().loading"
                [error]="search().error"
                [queried]="search().queried"
                (query)="onQuery($event)"
                (pick)="onPick($event)"
              />
            </div>
          </mat-tab>
        </mat-tab-group>
      </mat-card>

      <app-query-controls />

      <app-results-list />
    </div>
  `,
  styles: `
    .dp-builder {
      display: flex;
      flex-direction: column;
      gap: var(--dp-space-5);
    }

    .dp-builder__tab {
      padding: var(--dp-space-4) 0;
    }
  `,
})
export class SelectionBuilderComponent {
  private readonly catalog = inject(CatalogStore);
  private readonly selection = inject(SelectionStore);
  private readonly searchCategories = inject(SEARCH_CATEGORIES);
  private readonly searchDishes = inject(SEARCH_DISHES);
  private readonly events = inject(AnalyticsEventBuilders);
  private readonly emitter = inject(AnalyticsEmitterService);
  private readonly destroyRef = inject(DestroyRef);

  // ── Catalog (taxonomy browse) ───────────────────────────────────────────────
  readonly categories = computed<readonly CategoryDto[]>(() => this.catalog.categories() ?? []);
  readonly categoriesLoading = this.catalog.categoriesLoading;
  readonly categoriesError = this.catalog.categoriesError;
  readonly categoriesEmpty = this.catalog.categoriesEmpty;

  /** The category the user drilled into (id), or `undefined` at the top level. */
  private readonly openCategoryId = signal<string | undefined>(undefined);
  readonly openCategory = computed<CategoryDto | undefined>(() => {
    const id = this.openCategoryId();
    return id === undefined ? undefined : this.categories().find((c) => c.id === id);
  });

  readonly openDishes = computed<readonly DishDto[]>(() => {
    const id = this.openCategoryId();
    return id === undefined ? [] : (this.catalog.dishes(id)() ?? []);
  });
  readonly openDishesLoading = computed<boolean>(() => {
    const id = this.openCategoryId();
    return id !== undefined && this.catalog.dishesLoading(id)();
  });
  readonly openDishesError = computed<ApiError | undefined>(() => {
    const id = this.openCategoryId();
    return id === undefined ? undefined : this.catalog.dishesError(id)();
  });
  readonly openDishesEmpty = computed<boolean>(() => {
    const id = this.openCategoryId();
    return id !== undefined && this.catalog.dishesEmpty(id)();
  });

  // ── Selection (the user's current query) ────────────────────────────────────
  readonly selectedCategoryIds = computed<readonly string[]>(() =>
    this.selection
      .items()
      .filter(isCategorySelection)
      .map((i) => i.categoryId),
  );
  readonly selectedDishIds = computed<readonly string[]>(() =>
    this.selection
      .items()
      .filter(isDishSelection)
      .map((i) => i.dishId),
  );

  /**
   * Display names for dishes the user has put in the selection, captured at the moment of add/pick
   * (the only points where a dish's name is in hand). Used to label dish chips without reading the
   * catalog's per-category cache from inside a `computed` — that read lazily creates a cache entry
   * (a signal write) and is forbidden in a computed (NG0600). A dish whose name was not captured
   * falls back to its id; the selection itself is always the exact id.
   */
  private readonly dishNames = signal<ReadonlyMap<string, string>>(new Map());

  /** The selection chips, labelling categories from the loaded catalog and dishes from {@link dishNames}. */
  readonly chips = computed<readonly SelectionChip[]>(() =>
    this.selection.items().map((item) => this.toChip(item)),
  );

  // ── Prefix search ───────────────────────────────────────────────────────────
  private readonly query$ = new Subject<string>();
  /** The current prefix-search snapshot the template binds to (loading/error/queried). */
  protected readonly search = signal<SearchState>(EMPTY_SEARCH);

  /** The flattened pick-list the typeahead renders (categories then dishes), with selected flags. */
  readonly searchResults = computed<readonly PrefixSearchResult[]>(() => {
    const s = this.search();
    const selectedCats = new Set(this.selectedCategoryIds());
    const selectedDishes = new Set(this.selectedDishIds());
    const categoryRows: PrefixSearchResult[] = s.categories.map((c) => ({
      id: c.id,
      label: c.name,
      kind: 'category',
      selected: selectedCats.has(c.id),
    }));
    const dishRows: PrefixSearchResult[] = s.dishes.map((d) => ({
      id: d.id,
      label: d.canonicalName,
      kind: 'dish',
      selected: selectedDishes.has(d.id),
    }));
    return [...categoryRows, ...dishRows];
  });

  constructor() {
    // Warm the taxonomy cache once (a repeat read is a store-level no-op).
    this.catalog.loadCategories();
    this.wirePrefixSearch();
  }

  // ── Browse handlers ─────────────────────────────────────────────────────────

  onOpenCategory(category: CategoryDto): void {
    this.openCategoryId.set(category.id);
    this.catalog.loadDishes(category.id); // cached per-category — a no-op on re-open
  }

  onCloseCategory(): void {
    this.openCategoryId.set(undefined);
  }

  reloadCategories(): void {
    this.catalog.loadCategories(true);
  }

  reloadDishes(category: CategoryDto): void {
    this.catalog.loadDishes(category.id, true);
  }

  onAddCategory(category: CategoryDto): void {
    this.selection.addItem({ categoryId: category.id });
    this.emitSearch();
  }

  onAddDish(dish: DishDto): void {
    this.rememberDishName(dish.id, dish.canonicalName);
    this.selection.addItem({ dishId: dish.id });
    this.emitSearch();
  }

  // ── Prefix-search handlers ──────────────────────────────────────────────────

  onQuery(text: string): void {
    this.query$.next(text);
  }

  onPick(result: PrefixSearchResult): void {
    if (result.kind === 'category') {
      this.selection.addItem({ categoryId: result.id });
    } else {
      this.rememberDishName(result.id, result.label);
      this.selection.addItem({ dishId: result.id });
    }
    this.emitSearch();
  }

  // ── Selection handlers ──────────────────────────────────────────────────────

  onRemove(item: SelectedItem): void {
    this.selection.removeItem(item);
  }

  onClear(): void {
    this.selection.clearItems();
  }

  // ── Internals ───────────────────────────────────────────────────────────────

  /**
   * Wire the debounced prefix-search stream once. Each settled prefix runs both search ports
   * (categories + dishes) anchored and capped at {@link SEARCH_LIMIT}; a too-short prefix resets to
   * the empty state without a request. `switchMap` cancels a superseded prefix so only the latest
   * result lands, and a failed lookup surfaces the typed {@link ApiError} without breaking the stream.
   */
  private wirePrefixSearch(): void {
    this.query$
      .pipe(
        map((text) => text.trim()),
        debounceTime(SEARCH_DEBOUNCE_MS),
        distinctUntilChanged(),
        switchMap((prefix) => {
          if (prefix.length < MIN_PREFIX_LENGTH) {
            return of(EMPTY_SEARCH);
          }
          this.search.set({ ...EMPTY_SEARCH, queried: true, loading: true });
          return this.runSearch(prefix);
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((state) => this.search.set(state));
  }

  /** Run both prefix-search ports for one prefix and merge into a single {@link SearchState}. */
  private runSearch(prefix: string): Observable<SearchState> {
    return forkJoin([
      this.searchCategories.execute(prefix, SEARCH_LIMIT),
      this.searchDishes.execute(prefix, SEARCH_LIMIT),
    ]).pipe(
      map(
        ([categories, dishes]): SearchState => ({
          queried: true,
          loading: false,
          categories,
          dishes,
        }),
      ),
      catchError((error: ApiError) => of<SearchState>({ ...EMPTY_SEARCH, queried: true, error })),
    );
  }

  /** Resolve a selected item to a labeled chip (category from the catalog, dish from {@link dishNames}). */
  private toChip(item: SelectedItem): SelectionChip {
    if (isCategorySelection(item)) {
      const name = this.categories().find((c) => c.id === item.categoryId)?.name;
      return { item, kind: 'category', label: name ?? item.categoryId };
    }
    const name = this.dishNames().get(item.dishId);
    return { item, kind: 'dish', label: name ?? item.dishId };
  }

  /** Record a dish's display name (idempotent) so its chip can be labelled without touching the catalog cache. */
  private rememberDishName(dishId: string, name: string): void {
    if (this.dishNames().get(dishId) === name) {
      return;
    }
    this.dishNames.update((map) => new Map(map).set(dishId, name));
  }

  /**
   * Emit a `search` analytics event for the current selection through the single batching emitter
   * (best-effort, non-blocking — invariant #11: opaque session id, no PII; the emitter swallows any
   * ingest failure so the user flow never breaks on analytics).
   */
  private emitSearch(): void {
    // When the user has opted into approximate geo (invariant #11), the lat/lng ride as FIELDS on the
    // `search` event — there is no `geo` event kind. When opted out, `userGeo` is undefined and the
    // geo fields are omitted entirely.
    const geo = this.selection.userGeo();
    this.emitter.emit(
      this.events.search({
        categoryIds: this.selectedCategoryIds(),
        dishIds: this.selectedDishIds(),
        sortMode: this.selection.sort(),
        ...(geo === undefined ? {} : { geo }),
      }),
    );
  }
}
