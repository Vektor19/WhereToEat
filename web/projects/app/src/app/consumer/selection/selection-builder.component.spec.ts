import { TestBed, type ComponentFixture } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { EMPTY, of } from 'rxjs';
import {
  AnalyticsEmitterService,
  CatalogStore,
  ResultsStore,
  SelectionStore,
  SEARCH_CATEGORIES,
  SEARCH_DISHES,
  GET_CATEGORIES,
  GET_DISHES_BY_CATEGORY,
  INGEST_ANALYTICS,
  RECOMMEND,
  type CategoryDto,
  type DishDto,
  type RawEventBody,
} from 'core';

/** A recommend port that never emits — the builder embeds the query-controls container, which wires
 * the {@link ResultsStore}; these selection-flow specs never submit, so an inert port suffices. */
const INERT_RECOMMEND = { execute: () => EMPTY };

import { SelectionBuilderComponent } from './selection-builder.component';

const CATEGORIES: readonly CategoryDto[] = [
  { id: 'c-1', name: 'Перші страви' },
  { id: 'c-2', name: 'Фаст-фуд' },
];

const DISHES_C1: readonly DishDto[] = [
  { id: 'd-1', categoryId: 'c-1', canonicalName: 'Борщ' },
  { id: 'd-2', categoryId: 'c-1', canonicalName: 'Суп' },
];

const SEARCH_CATEGORY_HITS: readonly CategoryDto[] = [{ id: 'c-2', name: 'Фаст-фуд' }];
const SEARCH_DISH_HITS: readonly DishDto[] = [
  { id: 'd-3', categoryId: 'c-2', canonicalName: 'Бургер' },
];

describe('SelectionBuilderComponent', () => {
  let ingestCalls: number;
  let ingestedEvents: RawEventBody[];

  beforeEach(async () => {
    localStorage.clear();
    ingestCalls = 0;
    ingestedEvents = [];
    await TestBed.configureTestingModule({
      imports: [SelectionBuilderComponent],
      providers: [
        provideNoopAnimations(),
        CatalogStore,
        SelectionStore,
        ResultsStore,
        { provide: RECOMMEND, useValue: INERT_RECOMMEND },
        { provide: GET_CATEGORIES, useValue: { execute: () => of(CATEGORIES) } },
        { provide: GET_DISHES_BY_CATEGORY, useValue: { execute: () => of(DISHES_C1) } },
        { provide: SEARCH_CATEGORIES, useValue: { execute: () => of(SEARCH_CATEGORY_HITS) } },
        { provide: SEARCH_DISHES, useValue: { execute: () => of(SEARCH_DISH_HITS) } },
        {
          provide: INGEST_ANALYTICS,
          useValue: {
            execute: (events: readonly RawEventBody[]) => {
              ingestCalls += 1;
              ingestedEvents.push(...events);
              return of(undefined);
            },
          },
        },
      ],
    }).compileComponents();
  });

  function render(): ComponentFixture<SelectionBuilderComponent> {
    const fixture = TestBed.createComponent(SelectionBuilderComponent);
    fixture.detectChanges();
    return fixture;
  }

  /** Force the batching emitter to flush its buffer so the ingest stub records the queued events. */
  function flushAnalytics(): void {
    TestBed.inject(AnalyticsEmitterService).flush();
  }

  /**
   * Resolve the prefix-search input. It lives in the second (Search) tab; activate that tab first so
   * Material renders its body synchronously (the inactive tab body is attached via a portal on a
   * scheduled task that fake timers do not deterministically flush). Selecting the tab and running
   * change detection puts the input in the DOM.
   */
  function searchInput(fixture: ComponentFixture<SelectionBuilderComponent>): HTMLInputElement {
    const el = fixture.nativeElement as HTMLElement;
    const tabLabels = el.querySelectorAll<HTMLElement>('.mat-mdc-tab');
    tabLabels[1]?.click();
    fixture.detectChanges();
    const box = el.querySelector('[data-testid="prefix-search-input"]') as HTMLInputElement | null;
    if (box === null) {
      throw new Error('prefix-search input not rendered');
    }
    return box;
  }

  it('loads and lists the taxonomy categories on init', () => {
    const el = render().nativeElement as HTMLElement;
    expect(el.querySelector('[data-testid="category-list"]')).not.toBeNull();
    expect(el.querySelectorAll('[data-testid="category-list"] li').length).toBe(2);
  });

  it('browsing a category populates its dishes', () => {
    const fixture = render();
    const el = fixture.nativeElement as HTMLElement;
    (el.querySelector('[data-testid="category-c-1"]') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(el.querySelector('[data-testid="dish-list"]')).not.toBeNull();
    expect(el.querySelectorAll('[data-testid="dish-list"] li').length).toBe(2);
  });

  it('adding a whole category puts exactly one category item in the selection', () => {
    const fixture = render();
    const el = fixture.nativeElement as HTMLElement;
    const selection = TestBed.inject(SelectionStore);
    (el.querySelector('[data-testid="add-category-c-2"]') as HTMLButtonElement).click();
    expect(selection.items()).toEqual([{ categoryId: 'c-2' }]);
  });

  it('adding a specific dish puts exactly one dish item in the selection', () => {
    const fixture = render();
    const el = fixture.nativeElement as HTMLElement;
    const selection = TestBed.inject(SelectionStore);
    (el.querySelector('[data-testid="category-c-1"]') as HTMLButtonElement).click();
    fixture.detectChanges();
    (el.querySelector('[data-testid="dish-d-1"]') as HTMLButtonElement).click();
    expect(selection.items()).toEqual([{ dishId: 'd-1' }]);
  });

  it('reflects the selection as chips and removes/clears via the store', () => {
    const fixture = render();
    const el = fixture.nativeElement as HTMLElement;
    const selection = TestBed.inject(SelectionStore);

    (el.querySelector('[data-testid="add-category-c-2"]') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(el.querySelectorAll('mat-chip').length).toBe(1);
    expect(el.textContent).toContain('Фаст-фуд');

    (
      el.querySelector('[data-testid="selection-remove-category-c-2"]') as HTMLButtonElement
    ).click();
    fixture.detectChanges();
    expect(selection.items()).toEqual([]);
    expect(el.querySelector('[data-testid="selection-empty"]')).not.toBeNull();
  });

  it('debounces the prefix search and renders the merged category+dish results', () => {
    vi.useFakeTimers();
    try {
      const fixture = render();
      const el = fixture.nativeElement as HTMLElement;
      const box = searchInput(fixture);

      box.value = 'бур';
      box.dispatchEvent(new Event('input'));
      // Before the debounce window elapses nothing has been queried.
      vi.advanceTimersByTime(100);
      fixture.detectChanges();
      expect(el.querySelector('[data-testid="prefix-search-results"]')).toBeNull();

      vi.advanceTimersByTime(200); // cross the 250ms debounce
      fixture.detectChanges();
      const rows = el.querySelectorAll('[data-testid^="prefix-search-result-"]');
      expect(rows.length).toBe(2); // one category hit + one dish hit
      expect(el.textContent).toContain('Фаст-фуд');
      expect(el.textContent).toContain('Бургер');
    } finally {
      vi.useRealTimers();
    }
  });

  it('picking a prefix-search result adds the matching item and emits a search event', () => {
    vi.useFakeTimers();
    try {
      const fixture = render();
      const el = fixture.nativeElement as HTMLElement;
      const selection = TestBed.inject(SelectionStore);
      const box = searchInput(fixture);

      box.value = 'бур';
      box.dispatchEvent(new Event('input'));
      vi.advanceTimersByTime(300);
      fixture.detectChanges();

      (el.querySelector('[data-testid="prefix-search-result-d-3"]') as HTMLElement).click();
      expect(selection.items()).toEqual([{ dishId: 'd-3' }]);
      flushAnalytics();
      expect(ingestCalls).toBeGreaterThan(0); // a search analytics event was emitted on build
    } finally {
      vi.useRealTimers();
    }
  });

  it('caps every prefix lookup at the SEARCH_LIMIT (10), anchored and capped', () => {
    const categoryArgs: unknown[][] = [];
    const dishArgs: unknown[][] = [];
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [SelectionBuilderComponent],
      providers: [
        provideNoopAnimations(),
        CatalogStore,
        SelectionStore,
        ResultsStore,
        { provide: RECOMMEND, useValue: INERT_RECOMMEND },
        { provide: GET_CATEGORIES, useValue: { execute: () => of(CATEGORIES) } },
        { provide: GET_DISHES_BY_CATEGORY, useValue: { execute: () => of(DISHES_C1) } },
        {
          provide: SEARCH_CATEGORIES,
          useValue: {
            execute: (...args: unknown[]) => {
              categoryArgs.push(args);
              return of(SEARCH_CATEGORY_HITS);
            },
          },
        },
        {
          provide: SEARCH_DISHES,
          useValue: {
            execute: (...args: unknown[]) => {
              dishArgs.push(args);
              return of(SEARCH_DISH_HITS);
            },
          },
        },
        { provide: INGEST_ANALYTICS, useValue: { execute: () => of(undefined) } },
      ],
    });

    vi.useFakeTimers();
    try {
      const fixture = TestBed.createComponent(SelectionBuilderComponent);
      fixture.detectChanges();
      const box = searchInput(fixture);

      box.value = 'бур';
      box.dispatchEvent(new Event('input'));
      vi.advanceTimersByTime(300);
      fixture.detectChanges();

      expect(categoryArgs.length).toBe(1);
      expect(dishArgs.length).toBe(1);
      // Second arg is the limit cap (deterministic prefix, never a full-catalog scan).
      expect(categoryArgs[0][1]).toBe(10);
      expect(dishArgs[0][1]).toBe(10);
    } finally {
      vi.useRealTimers();
    }
  });

  it('a too-short prefix never queries (deterministic, no full-catalog scan)', () => {
    vi.useFakeTimers();
    try {
      const fixture = render();
      const el = fixture.nativeElement as HTMLElement;
      const box = searchInput(fixture);

      box.value = 'б';
      box.dispatchEvent(new Event('input'));
      vi.advanceTimersByTime(300);
      fixture.detectChanges();
      expect(el.querySelector('[data-testid="prefix-search-results"]')).toBeNull();
    } finally {
      vi.useRealTimers();
    }
  });

  it('stamps opt-in lat/lng onto the search event as FIELDS and never emits a `geo` kind (Step 15)', () => {
    const fixture = render();
    const el = fixture.nativeElement as HTMLElement;
    const selection = TestBed.inject(SelectionStore);

    // Simulate the geo opt-in having set the approximate coordinates on the selection store.
    selection.setGeo({ latitude: 50.45, longitude: 30.52 });

    (el.querySelector('[data-testid="add-category-c-2"]') as HTMLButtonElement).click();
    flushAnalytics();

    const search = ingestedEvents.find((e) => e.kind === 'search');
    expect(search).toBeDefined();
    expect(search?.latitude).toBe(50.45);
    expect(search?.longitude).toBe(30.52);
    // No `geo` event kind exists (the backend enum has none) — invariant #11.
    expect(ingestedEvents.some((e) => (e.kind as string) === 'geo')).toBe(false);
  });

  it('omits the geo fields from the search event when the user has not opted in', () => {
    const fixture = render();
    const el = fixture.nativeElement as HTMLElement;

    (el.querySelector('[data-testid="add-category-c-2"]') as HTMLButtonElement).click();
    flushAnalytics();

    const search = ingestedEvents.find((e) => e.kind === 'search');
    expect(search).toBeDefined();
    expect(search?.latitude).toBeUndefined();
    expect(search?.longitude).toBeUndefined();
  });
});
