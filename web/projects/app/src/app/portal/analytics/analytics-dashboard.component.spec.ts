import { TestBed, type ComponentFixture } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { Subject, of, throwError, type Observable } from 'rxjs';

import {
  GET_DEMAND,
  GET_FUNNEL,
  GET_PRICE_POSITIONING,
  GET_RATINGS_DISTRIBUTION,
  GET_TRAFFIC,
  type ApiError,
  type ConversionFunnelDto,
  type DemandBreakdownDto,
  type PricePositioningDto,
  type RatingsDistributionDto,
  type RestaurantTrafficSummaryDto,
} from 'core';

import { AnalyticsDashboardComponent } from './analytics-dashboard.component';

const TRAFFIC: RestaurantTrafficSummaryDto = {
  restaurantId: 'r-1',
  impressions: 1000,
  cardOpens: 120,
  actions: 30,
  ctr: 0.12,
};
const FUNNEL: ConversionFunnelDto = {
  restaurantId: 'r-1',
  impressions: 1000,
  cardOpens: 120,
  actions: 30,
  impressionToCardOpenRate: 0.12,
  cardOpenToActionRate: 0.25,
};
const DEMAND: DemandBreakdownDto = {
  categories: [{ selectionId: 'cat-1', searchCount: 50 }],
  dishes: [{ selectionId: 'dish-1', searchCount: 40 }],
};
const PRICE: PricePositioningDto = {
  restaurantId: 'r-1',
  dishes: [
    { dishId: 'dish-1', venuePrice: 150, medianPrice: 170, deltaFromMedian: -20, currency: 'UAH' },
  ],
};
const RATINGS: RatingsDistributionDto = {
  restaurantId: 'r-1',
  ratingCount: 42,
  scoreSum: 189,
  averageScore: 4.5,
};

const SERVER_ERROR: ApiError = {
  kind: 'server',
  status: 500,
  code: 'ServerError',
  message: 'boom',
  messageKey: 'errors.server',
};

interface Calls {
  traffic: { id: string; from: string; to: string }[];
  funnel: { id: string; from: string; to: string }[];
  demand: { from: string; to: string; top?: number }[];
  price: string[];
  ratings: string[];
}

describe('AnalyticsDashboardComponent', () => {
  let calls: Calls;
  let trafficResult: () => Observable<RestaurantTrafficSummaryDto>;

  beforeEach(async () => {
    calls = { traffic: [], funnel: [], demand: [], price: [], ratings: [] };
    trafficResult = () => of(TRAFFIC);

    await TestBed.configureTestingModule({
      imports: [AnalyticsDashboardComponent],
      providers: [
        provideNoopAnimations(),
        {
          provide: GET_TRAFFIC,
          useValue: {
            execute: (id: string, from: string, to: string) => {
              calls.traffic.push({ id, from, to });
              return trafficResult();
            },
          },
        },
        {
          provide: GET_FUNNEL,
          useValue: {
            execute: (id: string, from: string, to: string) => {
              calls.funnel.push({ id, from, to });
              return of(FUNNEL);
            },
          },
        },
        {
          provide: GET_DEMAND,
          useValue: {
            execute: (from: string, to: string, top?: number) => {
              calls.demand.push({ from, to, top });
              return of(DEMAND);
            },
          },
        },
        {
          provide: GET_PRICE_POSITIONING,
          useValue: {
            execute: (id: string) => {
              calls.price.push(id);
              return of(PRICE);
            },
          },
        },
        {
          provide: GET_RATINGS_DISTRIBUTION,
          useValue: {
            execute: (id: string) => {
              calls.ratings.push(id);
              return of(RATINGS);
            },
          },
        },
      ],
    }).compileComponents();
  });

  function render(): ComponentFixture<AnalyticsDashboardComponent> {
    const fixture = TestBed.createComponent(AnalyticsDashboardComponent);
    fixture.detectChanges();
    return fixture;
  }

  function el(
    fixture: ComponentFixture<AnalyticsDashboardComponent>,
    testid: string,
  ): Element | null {
    return fixture.nativeElement.querySelector(`[data-testid="${testid}"]`);
  }

  function loadFor(
    fixture: ComponentFixture<AnalyticsDashboardComponent>,
    restaurantId = 'r-1',
  ): void {
    fixture.componentInstance.form.patchValue({ restaurantId });
    fixture.detectChanges();
    fixture.componentInstance.load(new Event('submit'));
    fixture.detectChanges();
  }

  it('shows the pre-load hint before any load, and renders nothing else', () => {
    const fixture = render();
    expect(el(fixture, 'analytics-pre-load')).not.toBeNull();
    expect(el(fixture, 'analytics-traffic')).toBeNull();
  });

  it('renders all five §7.5 metric families after a load for a restaurant + window', () => {
    const fixture = render();
    loadFor(fixture);

    expect(el(fixture, 'analytics-traffic')).not.toBeNull();
    expect(el(fixture, 'analytics-funnel')).not.toBeNull();
    expect(el(fixture, 'analytics-demand')).not.toBeNull();
    expect(el(fixture, 'analytics-price')).not.toBeNull();
    expect(el(fixture, 'analytics-ratings')).not.toBeNull();

    // The traffic figures come straight from the aggregate DTO.
    expect(el(fixture, 'traffic-impressions')?.textContent).toContain('1000');
    expect(el(fixture, 'ratings-average')?.textContent).toContain('4.5');
  });

  it('targets the admin reads with the chosen restaurant id + an ISO window', () => {
    const fixture = render();
    loadFor(fixture, 'venue-7');

    expect(calls.traffic).toHaveLength(1);
    expect(calls.traffic[0].id).toBe('venue-7');
    // The container converts the datetime-local form values to ISO instants for the API.
    expect(calls.traffic[0].from).toMatch(/T.*Z$/);
    expect(calls.funnel[0].id).toBe('venue-7');
    // Demand is area-wide (no restaurant id) but still window-scoped.
    expect(calls.demand[0].from).toBe(calls.traffic[0].from);
    expect(calls.price).toEqual(['venue-7']);
    expect(calls.ratings).toEqual(['venue-7']);
  });

  it('shows the typed error for a failing metric without blanking the others', () => {
    trafficResult = () => throwError(() => SERVER_ERROR);
    const fixture = render();
    loadFor(fixture);

    // The traffic panel surfaces the typed error; the other panels still render.
    expect(el(fixture, 'analytics-traffic')).toBeNull();
    expect(el(fixture, 'error-state')).not.toBeNull();
    expect(el(fixture, 'analytics-funnel')).not.toBeNull();
    expect(el(fixture, 'analytics-ratings')).not.toBeNull();
  });

  it('shows a loading state for an in-flight metric query', () => {
    const pending = new Subject<RestaurantTrafficSummaryDto>();
    trafficResult = () => pending.asObservable();
    const fixture = render();
    loadFor(fixture);

    // Traffic is still loading; the shared loading primitive renders.
    expect(el(fixture, 'analytics-traffic')).toBeNull();
    expect(fixture.nativeElement.querySelector('[data-testid="loading"]')).not.toBeNull();

    pending.next(TRAFFIC);
    pending.complete();
    fixture.detectChanges();
    expect(el(fixture, 'analytics-traffic')).not.toBeNull();
  });

  it('blocks loading until a restaurant id is entered (form invalid)', () => {
    const fixture = render();
    // No restaurant id patched — load is a no-op and the grid never appears.
    fixture.componentInstance.load(new Event('submit'));
    fixture.detectChanges();
    expect(el(fixture, 'analytics-pre-load')).not.toBeNull();
    expect(calls.traffic).toHaveLength(0);
  });

  it('surfaces the aggregates-only privacy note (invariant #11)', () => {
    const fixture = render();
    expect(el(fixture, 'analytics-privacy')).not.toBeNull();
  });
});
