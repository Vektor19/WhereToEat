import { TestBed, type ComponentFixture } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { Subject, of } from 'rxjs';
import {
  AnalyticsEmitterService,
  RECOMMEND,
  INGEST_ANALYTICS,
  ResultsStore,
  SelectionStore,
  type RecommendationRequest,
  type RecommendationResultDto,
} from 'core';

import { QueryControlsComponent } from './query-controls.component';

const EMPTY_RESULT: RecommendationResultDto = { match: 'or', sort: 'best', restaurants: [] };

describe('QueryControlsComponent', () => {
  let submitted: RecommendationRequest[];
  let ingestCalls: number;
  let recommend$: Subject<RecommendationResultDto>;

  beforeEach(async () => {
    localStorage.clear();
    submitted = [];
    ingestCalls = 0;
    recommend$ = new Subject<RecommendationResultDto>();

    await TestBed.configureTestingModule({
      imports: [QueryControlsComponent],
      providers: [
        provideNoopAnimations(),
        SelectionStore,
        ResultsStore,
        {
          provide: RECOMMEND,
          useValue: {
            execute: (request: RecommendationRequest) => {
              submitted.push(request);
              return recommend$.asObservable();
            },
          },
        },
        {
          provide: INGEST_ANALYTICS,
          useValue: {
            execute: () => {
              ingestCalls += 1;
              return of(undefined);
            },
          },
        },
      ],
    }).compileComponents();
  });

  function render(): ComponentFixture<QueryControlsComponent> {
    const fixture = TestBed.createComponent(QueryControlsComponent);
    fixture.detectChanges();
    return fixture;
  }

  function submitButton(fixture: ComponentFixture<QueryControlsComponent>): HTMLButtonElement {
    return fixture.nativeElement.querySelector('[data-testid="query-submit"]') as HTMLButtonElement;
  }

  it('changing the match toggle updates the selection store and the next request', () => {
    const fixture = render();
    const selection = TestBed.inject(SelectionStore);
    selection.addItem({ dishId: 'd-1' });

    (
      fixture.nativeElement.querySelector('[data-testid="match-and"] button') as HTMLButtonElement
    ).click();
    expect(selection.match()).toBe('and');

    fixture.detectChanges();
    submitButton(fixture).click();
    expect(submitted[0].match).toBe('and');
  });

  it('changing the sort sets each mode on the store and feeds the next request', () => {
    const fixture = render();
    const selection = TestBed.inject(SelectionStore);
    selection.addItem({ dishId: 'd-1' });

    (
      fixture.nativeElement.querySelector('[data-testid="sort-price"] button') as HTMLButtonElement
    ).click();
    expect(selection.sort()).toBe('price');

    fixture.detectChanges();
    submitButton(fixture).click();
    expect(submitted[0].sort).toBe('price');
  });

  it('applies and clears price/rating filters on the store and emits a filter event', () => {
    const fixture = render();
    const selection = TestBed.inject(SelectionStore);

    const price = fixture.nativeElement.querySelector(
      '[data-testid="filter-input-price"]',
    ) as HTMLInputElement;
    price.value = '150';
    price.dispatchEvent(new Event('input'));
    expect(selection.filters()).toEqual([{ key: 'price', value: '150' }]);
    TestBed.inject(AnalyticsEmitterService).flush();
    expect(ingestCalls).toBeGreaterThan(0);

    const rating = fixture.nativeElement.querySelector(
      '[data-testid="filter-input-rating"]',
    ) as HTMLInputElement;
    rating.value = '4';
    rating.dispatchEvent(new Event('input'));
    expect(selection.filters()).toEqual([
      { key: 'price', value: '150' },
      { key: 'rating', value: '4' },
    ]);

    // Clearing the price input removes only that filter.
    price.value = '';
    price.dispatchEvent(new Event('input'));
    expect(selection.filters()).toEqual([{ key: 'rating', value: '4' }]);
  });

  it('disables submit on an empty selection and never submits an invalid request', () => {
    const fixture = render();
    expect(submitButton(fixture).disabled).toBe(true);

    // Force a click even while disabled — the build is invalid (empty selection) so nothing submits.
    fixture.componentInstance.onSubmit();
    expect(submitted.length).toBe(0);
    fixture.detectChanges();
    const validation = fixture.nativeElement.querySelector('[data-testid="query-validation"]');
    expect(validation).not.toBeNull();
    expect(
      fixture.nativeElement.querySelector('[data-testid="query-error-Recommend.EmptySelection"]'),
    ).not.toBeNull();
  });

  it('builds the exact wire request (items + match + sort + filters) and submits via the results store', () => {
    const fixture = render();
    const selection = TestBed.inject(SelectionStore);
    selection.addItem({ dishId: 'd-1' });
    selection.addItem({ categoryId: 'c-2' });

    (
      fixture.nativeElement.querySelector('[data-testid="sort-rating"] button') as HTMLButtonElement
    ).click();
    const price = fixture.nativeElement.querySelector(
      '[data-testid="filter-input-price"]',
    ) as HTMLInputElement;
    price.value = '120';
    price.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    submitButton(fixture).click();

    expect(submitted.length).toBe(1);
    expect(submitted[0]).toEqual({
      items: [{ dishId: 'd-1' }, { categoryId: 'c-2' }],
      match: 'or',
      sort: 'rating',
      filters: [{ key: 'price', value: '120' }],
    });
  });

  it('shows the loading state (disabled submit) while the request is in flight, then resolves', () => {
    const fixture = render();
    const selection = TestBed.inject(SelectionStore);
    const results = TestBed.inject(ResultsStore);
    selection.addItem({ dishId: 'd-1' });
    fixture.detectChanges();

    submitButton(fixture).click();
    fixture.detectChanges();
    expect(results.loadingResults()).toBe(true);
    expect(submitButton(fixture).disabled).toBe(true);

    recommend$.next(EMPTY_RESULT);
    recommend$.complete();
    fixture.detectChanges();
    expect(results.loadingResults()).toBe(false);
    expect(results.loaded()).toBe(true);
  });
});
