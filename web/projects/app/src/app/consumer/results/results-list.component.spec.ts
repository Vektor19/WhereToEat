import { TestBed, type ComponentFixture } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { Router, provideRouter } from '@angular/router';
import { Subject, of } from 'rxjs';
import {
  AnalyticsEmitterService,
  GET_MAP,
  MAP_PROVIDER,
  RECOMMEND,
  INGEST_ANALYTICS,
  ResultsStore,
  type MapPayloadDto,
  type MapView,
  type RawEventBody,
  type RecommendationRequest,
  type RecommendationResultDto,
  type RecommendedRestaurantDto,
} from 'core';

import { ResultsListComponent } from './results-list.component';

function restaurant(over: Partial<RecommendedRestaurantDto>): RecommendedRestaurantDto {
  return {
    restaurantId: 'r',
    name: 'R',
    basketPriceAmount: 100,
    basketPriceCurrency: 'UAH',
    smoothedRating: 4.5,
    ratingCount: 40,
    distanceKm: null,
    coverage: 1,
    ...over,
  };
}

describe('ResultsListComponent', () => {
  let recommend$: Subject<RecommendationResultDto>;
  let ingestBatches: RawEventBody[][];
  let map$: Subject<MapPayloadDto>;

  beforeEach(async () => {
    recommend$ = new Subject<RecommendationResultDto>();
    ingestBatches = [];
    map$ = new Subject<MapPayloadDto>();

    await TestBed.configureTestingModule({
      imports: [ResultsListComponent],
      providers: [
        provideNoopAnimations(),
        // The card-open handler navigates to the details route (Step 14); a router is required.
        provideRouter([]),
        ResultsStore,
        {
          provide: RECOMMEND,
          useValue: { execute: () => recommend$.asObservable() },
        },
        {
          provide: INGEST_ANALYTICS,
          useValue: {
            execute: (events: readonly RawEventBody[]) => {
              ingestBatches.push([...events]);
              return of(undefined);
            },
          },
        },
        // The hosted map modal injects the GET_MAP port + the active map provider.
        { provide: GET_MAP, useValue: { execute: () => map$.asObservable() } },
        {
          provide: MAP_PROVIDER,
          useValue: { resolve: (): MapView => ({ kind: 'none' }) },
        },
      ],
    }).compileComponents();
  });

  function render(): {
    fixture: ComponentFixture<ResultsListComponent>;
    store: ResultsStore;
  } {
    const fixture = TestBed.createComponent(ResultsListComponent);
    const store = TestBed.inject(ResultsStore);
    fixture.detectChanges();
    return { fixture, store };
  }

  function submit(
    store: ResultsStore,
    result: RecommendationResultDto,
    items: RecommendationRequest['items'] = [{ dishId: 'd-1' }],
  ): void {
    store.submit({ items, match: 'or', sort: 'price', filters: [] });
    recommend$.next(result);
    recommend$.complete();
  }

  function names(fixture: ComponentFixture<ResultsListComponent>): string[] {
    return Array.from(
      fixture.nativeElement.querySelectorAll('[data-testid="result-name"]') as NodeListOf<Element>,
    ).map((el) => (el.textContent ?? '').trim());
  }

  it('renders cards in the exact backend order (never re-sorted — invariant #5)', () => {
    const { fixture, store } = render();
    submit(store, {
      match: 'or',
      sort: 'price',
      restaurants: [
        restaurant({ restaurantId: 'a', name: 'Alpha', basketPriceAmount: 300 }),
        restaurant({ restaurantId: 'b', name: 'Bravo', basketPriceAmount: 100 }),
        restaurant({ restaurantId: 'c', name: 'Charlie', basketPriceAmount: 200 }),
      ],
    });
    fixture.detectChanges();
    // Order preserved as received (not re-sorted by price ascending).
    expect(names(fixture)).toEqual(['Alpha', 'Bravo', 'Charlie']);
  });

  it('formats the basket price by the DTO currency (no hardcoded symbol)', () => {
    const { fixture, store } = render();
    submit(store, {
      match: 'or',
      sort: 'price',
      restaurants: [
        restaurant({
          restaurantId: 'a',
          name: 'Euro',
          basketPriceAmount: 12,
          basketPriceCurrency: 'EUR',
        }),
      ],
    });
    fixture.detectChanges();
    const price = (
      fixture.nativeElement.querySelector('[data-testid="result-price"]')?.textContent ?? ''
    ).trim();
    expect(price.length).toBeGreaterThan(0);
    // Euro formatting must not carry the ₴ symbol — formatting is currency-driven, not hardcoded.
    expect(price).not.toContain('₴');
  });

  it('shows the low-review note below threshold and not above', () => {
    const { fixture, store } = render();
    submit(store, {
      match: 'or',
      sort: 'rating',
      restaurants: [
        restaurant({ restaurantId: 'low', name: 'Low', smoothedRating: 5, ratingCount: 2 }),
        restaurant({ restaurantId: 'high', name: 'High', smoothedRating: 4.5, ratingCount: 80 }),
      ],
    });
    fixture.detectChanges();
    const notes = fixture.nativeElement.querySelectorAll('[data-testid="result-rating-note"]');
    // Exactly one card (the low-review one) shows the discreet note.
    expect(notes.length).toBe(1);
  });

  it('shows distance only when geo was provided', () => {
    const { fixture, store } = render();
    submit(store, {
      match: 'or',
      sort: 'distance',
      restaurants: [
        restaurant({ restaurantId: 'near', name: 'Near', distanceKm: 1.2 }),
        restaurant({ restaurantId: 'noGeo', name: 'NoGeo', distanceKm: null }),
      ],
    });
    fixture.detectChanges();
    const distances = fixture.nativeElement.querySelectorAll('[data-testid="result-distance"]');
    expect(distances.length).toBe(1);
  });

  it('renders coverage "N of M" using the submitted selection size as the denominator', () => {
    const { fixture, store } = render();
    submit(
      store,
      {
        match: 'or',
        sort: 'price',
        restaurants: [restaurant({ restaurantId: 'a', name: 'A', coverage: 1 })],
      },
      [{ dishId: 'd-1' }, { categoryId: 'c-2' }],
    );
    fixture.detectChanges();
    const coverage = (
      fixture.nativeElement.querySelector('[data-testid="result-coverage"]')?.textContent ?? ''
    ).trim();
    expect(coverage).toContain('1');
    expect(coverage).toContain('2');
  });

  it('emits one impression per shown card on a successful load and card_open on open', () => {
    const { fixture, store } = render();
    submit(store, {
      match: 'or',
      sort: 'price',
      restaurants: [
        restaurant({ restaurantId: 'a', name: 'A' }),
        restaurant({ restaurantId: 'b', name: 'B' }),
      ],
    });
    fixture.detectChanges();
    const emitter = TestBed.inject(AnalyticsEmitterService);
    emitter.flush();

    const impressionBatch = ingestBatches.find((b) => b.every((e) => e.kind === 'impression'));
    expect(impressionBatch).toBeDefined();
    expect(impressionBatch?.length).toBe(2);
    expect(impressionBatch?.map((e) => e.restaurantId)).toEqual(['a', 'b']);

    const router = TestBed.inject(Router);
    const navigate = vi.spyOn(router, 'navigate').mockResolvedValue(true);

    (
      fixture.nativeElement.querySelector(
        '[data-testid="result-card-a"] [data-testid="result-open"]',
      ) as HTMLButtonElement
    ).click();
    emitter.flush();
    const cardOpen = ingestBatches.flat().find((e) => e.kind === 'card_open');
    expect(cardOpen?.restaurantId).toBe('a');
    // Card-open navigates to the restaurant-details route (Step 14).
    expect(navigate).toHaveBeenCalledWith(['restaurant', 'a']);
  });

  it('does not re-impress the same cards when two submits differ only by geo opt-in', () => {
    const { fixture, store } = render();
    const restaurants = [
      restaurant({ restaurantId: 'a', name: 'A' }),
      restaurant({ restaurantId: 'b', name: 'B' }),
    ];
    const result: RecommendationResultDto = { match: 'or', sort: 'price', restaurants };
    const items: RecommendationRequest['items'] = [{ dishId: 'd-1' }];
    const emitter = TestBed.inject(AnalyticsEmitterService);

    // First submit: geo opted out (no userGeo) → both cards impressed once.
    store.submit({ items, match: 'or', sort: 'price', filters: [] });
    recommend$.next(result);
    fixture.detectChanges();

    // Second submit: identical result-set identity, only geo opt-in differs (userGeo added).
    // The RECOMMEND stub closes over the `recommend$` `let`, so a fresh subject is picked up.
    recommend$ = new Subject<RecommendationResultDto>();
    store.submit({
      items,
      match: 'or',
      sort: 'price',
      filters: [],
      userGeo: { latitude: 50.45, longitude: 30.52 },
    });
    recommend$.next(result);
    fixture.detectChanges();

    emitter.flush();
    const impressions = ingestBatches.flat().filter((e) => e.kind === 'impression');
    // Toggling geo must NOT open a fresh dedupe scope: each card is impressed exactly once total.
    expect(impressions.length).toBe(2);
    expect(impressions.map((e) => e.restaurantId).sort()).toEqual(['a', 'b']);
  });

  it('renders the loading state while the request is in flight', () => {
    const { fixture, store } = render();
    store.submit({ items: [{ dishId: 'd-1' }], match: 'or', sort: 'price', filters: [] });
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[data-testid="loading"]')).not.toBeNull();
  });

  it('renders the empty state when the engine returns no restaurants', () => {
    const { fixture, store } = render();
    submit(store, { match: 'or', sort: 'price', restaurants: [] });
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[data-testid="error-state"]')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('[data-testid="results-list"]')).toBeNull();
  });

  it('renders the error state when the request fails', () => {
    const { fixture, store } = render();
    store.submit({ items: [{ dishId: 'd-1' }], match: 'or', sort: 'price', filters: [] });
    recommend$.error({
      kind: 'server',
      status: 500,
      code: 'Server.Error',
      message: 'boom',
      messageKey: 'errors.server',
    });
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[data-testid="error-state"]')).not.toBeNull();
  });

  function manyRestaurants(count: number): RecommendedRestaurantDto[] {
    return Array.from({ length: count }, (_v, i) =>
      restaurant({ restaurantId: `r-${i}`, name: `R${i}` }),
    );
  }

  it('switches to the CDK virtual viewport once the list exceeds the virtualize threshold', () => {
    const { fixture, store } = render();
    // 21 > VIRTUALIZE_THRESHOLD (20) → virtual viewport path.
    submit(store, { match: 'or', sort: 'price', restaurants: manyRestaurants(21) });
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[data-testid="results-virtual"]')).not.toBeNull();
  });

  it('renders inline (no virtual viewport) at or below the virtualize threshold', () => {
    const { fixture, store } = render();
    // 20 (the boundary) is NOT > VIRTUALIZE_THRESHOLD → inline @for path.
    submit(store, { match: 'or', sort: 'price', restaurants: manyRestaurants(20) });
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[data-testid="results-virtual"]')).toBeNull();
    // The inline path still renders the list region with its cards.
    expect(fixture.nativeElement.querySelector('[data-testid="results-list"]')).not.toBeNull();
    expect(names(fixture).length).toBe(20);
  });

  it('opens the inline map modal from a card without leaving the list (§5.7)', () => {
    const { fixture, store } = render();
    submit(store, {
      match: 'or',
      sort: 'price',
      restaurants: [restaurant({ restaurantId: 'a', name: 'A' })],
    });
    fixture.detectChanges();
    // No modal until the affordance is used.
    expect(fixture.nativeElement.querySelector('[data-testid="map-dialog"]')).toBeNull();

    (
      fixture.nativeElement.querySelector(
        '[data-testid="result-card-a"] [data-testid="result-view-map"]',
      ) as HTMLButtonElement
    ).click();
    fixture.detectChanges();

    // The modal opened inline; the results list region is still present (no navigation).
    expect(fixture.nativeElement.querySelector('[data-testid="map-dialog"]')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('[data-testid="results-list"]')).not.toBeNull();
  });

  it('returns focus to the view-on-map affordance when the modal closes (WCAG)', () => {
    const { fixture, store } = render();
    submit(store, {
      match: 'or',
      sort: 'price',
      restaurants: [restaurant({ restaurantId: 'a', name: 'A' })],
    });
    fixture.detectChanges();

    const trigger = fixture.nativeElement.querySelector(
      '[data-testid="result-card-a"] [data-testid="result-view-map"]',
    ) as HTMLButtonElement;
    trigger.focus();
    trigger.click();
    fixture.detectChanges();

    (fixture.nativeElement.querySelector('[data-testid="map-close"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="map-dialog"]')).toBeNull();
    expect(document.activeElement).toBe(trigger);
  });

  it('does not render an ad slot for the organic backend flow (no ad field today)', () => {
    const { fixture, store } = render();
    submit(store, {
      match: 'or',
      sort: 'price',
      restaurants: [restaurant({ restaurantId: 'a', name: 'A' })],
    });
    fixture.detectChanges();
    // The recommend response has no ad-slot field, so every card is organic — no labeled ad chrome.
    expect(fixture.nativeElement.querySelector('[data-testid="ad-slot"]')).toBeNull();
  });
});
