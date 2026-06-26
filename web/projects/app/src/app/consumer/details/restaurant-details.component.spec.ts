import { signal, type Signal } from '@angular/core';
import { TestBed, type ComponentFixture } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { Subject, of } from 'rxjs';
import {
  AnalyticsEmitterService,
  AnalyticsEventBuilders,
  AuthService,
  CatalogStore,
  GET_RESTAURANT_DETAILS,
  INGEST_ANALYTICS,
  SUBMIT_RATING,
  type RawEventBody,
  type RestaurantDetailsDto,
} from 'core';

import { RestaurantDetailsComponent } from './restaurant-details.component';

/** A minimal {@link AuthService} stub: the child rating control only reads `isAuthenticated`. */
class StubAuthService {
  readonly isAuthenticated = signal(false);
  login = (): Promise<void> => Promise.resolve();
}

/**
 * A minimal {@link CatalogStore} stub: its loaded taxonomy holds only the known dish id `d-1` ("Борщ"),
 * so the container's name-resolution (a fetch-free scan of `state().dishesByCategory`) and its
 * neutral-fallback path for any other id are both exercised without pulling the real store (and its
 * `APP_CONFIG`/HTTP graph) into the test. Only the `state()` slice the component reads is provided.
 */
/** The slice of the catalog `state()` the details component reads (a per-category dishes map). */
interface StubDishEntry {
  readonly data: Signal<readonly { id: string; categoryId: string; canonicalName: string }[]>;
}
interface StubCatalogState {
  readonly dishesByCategory: ReadonlyMap<string, StubDishEntry>;
}

class StubCatalogStore {
  readonly state: Signal<StubCatalogState> = signal({
    dishesByCategory: new Map<string, StubDishEntry>([
      ['cat-1', { data: signal([{ id: 'd-1', categoryId: 'cat-1', canonicalName: 'Борщ' }]) }],
    ]),
  });
}

function details(over: Partial<RestaurantDetailsDto> = {}): RestaurantDetailsDto {
  return {
    id: 'r-1',
    name: 'Борщ House',
    addressLine: 'вул. Хрещатик, 1',
    addressCity: 'Київ',
    contactLinks: [],
    menuItems: [],
    ...over,
  };
}

describe('RestaurantDetailsComponent', () => {
  let details$: Subject<RestaurantDetailsDto>;
  let ingestBatches: RawEventBody[][];

  beforeEach(async () => {
    details$ = new Subject<RestaurantDetailsDto>();
    ingestBatches = [];

    await TestBed.configureTestingModule({
      imports: [RestaurantDetailsComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        AnalyticsEventBuilders,
        { provide: CatalogStore, useClass: StubCatalogStore },
        { provide: AuthService, useClass: StubAuthService },
        { provide: SUBMIT_RATING, useValue: { execute: () => of(undefined) } },
        { provide: GET_RESTAURANT_DETAILS, useValue: { execute: () => details$.asObservable() } },
        {
          provide: INGEST_ANALYTICS,
          useValue: {
            execute: (events: readonly RawEventBody[]) => {
              ingestBatches.push([...events]);
              return of(undefined);
            },
          },
        },
      ],
    }).compileComponents();
  });

  function render(id = 'r-1'): ComponentFixture<RestaurantDetailsComponent> {
    const fixture = TestBed.createComponent(RestaurantDetailsComponent);
    fixture.componentRef.setInput('id', id);
    fixture.detectChanges();
    return fixture;
  }

  function el(
    fixture: ComponentFixture<RestaurantDetailsComponent>,
    testid: string,
  ): Element | null {
    return fixture.nativeElement.querySelector(`[data-testid="${testid}"]`);
  }

  function landed(
    fixture: ComponentFixture<RestaurantDetailsComponent>,
    dto: RestaurantDetailsDto,
  ): void {
    details$.next(dto);
    details$.complete();
    fixture.detectChanges();
  }

  it('shows loading until the payload lands, then renders name and address', () => {
    const fixture = render();
    expect(el(fixture, 'loading')).not.toBeNull();

    landed(fixture, details());
    expect((el(fixture, 'details-name')?.textContent ?? '').trim()).toBe('Борщ House');
    expect(el(fixture, 'details-address')?.textContent).toContain('вул. Хрещатик, 1');
    expect(el(fixture, 'details-address')?.textContent).toContain('Київ');
  });

  it('renders the not-found state on a 404 (typed not-found)', () => {
    const fixture = render();
    details$.error({
      kind: 'not-found',
      status: 404,
      code: 'NotFound',
      message: 'missing',
      messageKey: 'errors.notFound',
    });
    fixture.detectChanges();
    expect(el(fixture, 'error-state')).not.toBeNull();
    expect(el(fixture, 'restaurant-details')).toBeNull();
  });

  it('ALWAYS renders the contact links even for a non-Verified venue (§5.8 / invariant #10)', () => {
    const fixture = render();
    // A plain (non-Verified) venue still carries contact links in the read model.
    landed(
      fixture,
      details({
        contactLinks: [
          { kind: 'site', url: 'https://venue.example', label: 'Сайт' },
          { kind: 'instagram', url: 'https://instagram.com/venue', label: null },
        ],
      }),
    );

    const links = fixture.nativeElement.querySelectorAll(
      '[data-testid="contact-link"]',
    ) as NodeListOf<HTMLAnchorElement>;
    expect(links.length).toBe(2);
    // No paywall — the links are present and clickable as safe external links.
    expect(links[0].getAttribute('href')).toBe('https://venue.example');
    expect(links[0].getAttribute('rel')).toBe('noopener noreferrer');
  });

  it('falls back to the kind-derived text (and a non-empty aria-label) for an empty label (WCAG 4.1.2)', () => {
    const fixture = render();
    // A backend that sends `label: ""` must not leave the visible text — nor the derived aria-label —
    // empty: the kind-derived fallback ("site") fills both.
    landed(
      fixture,
      details({ contactLinks: [{ kind: 'site', url: 'https://venue.example', label: '' }] }),
    );

    const link = el(fixture, 'contact-link') as HTMLAnchorElement;
    // The visible text lives in its own span (the anchor also holds an aria-hidden icon glyph).
    const visible = (link.querySelector('.dp-contacts__text')?.textContent ?? '').trim();
    expect(visible).toBe('site');
    const aria = link.getAttribute('aria-label') ?? '';
    expect(aria.trim().length).toBeGreaterThan(0);
    expect(aria).toBe('site');
  });

  it('formats menu prices by the item priceCurrency (no hardcoded symbol)', () => {
    const fixture = render();
    landed(
      fixture,
      details({
        menuItems: [
          { id: 'm-1', dishId: 'd-1', priceAmount: 12, priceCurrency: 'EUR', weight: '300 г' },
        ],
      }),
    );
    const price = (el(fixture, 'menu-item-price')?.textContent ?? '').trim();
    expect(price.length).toBeGreaterThan(0);
    // EUR formatting must not carry the ₴ symbol — currency is data-driven.
    expect(price).not.toContain('₴');
    expect(el(fixture, 'menu-item-weight')?.textContent).toContain('300 г');
  });

  it('renders generic menu photos by default and no per-item disclaimer (invariant #8 / #12)', () => {
    const fixture = render();
    landed(
      fixture,
      details({
        menuItems: [{ id: 'm-1', dishId: 'd-1', priceAmount: 100, priceCurrency: 'UAH' }],
      }),
    );
    expect(el(fixture, 'generic-photo')).not.toBeNull();
    expect(el(fixture, 'real-photo')).toBeNull();
    const text = fixture.nativeElement.textContent ?? '';
    expect(text).not.toContain('≈');
    expect(text.toLowerCase()).not.toContain('оновлено');
  });

  it('emits view and card_open analytics once on a successful load', () => {
    const fixture = render();
    landed(fixture, details());
    TestBed.inject(AnalyticsEmitterService).flush();
    const kinds = ingestBatches.flat().map((e) => e.kind);
    expect(kinds.filter((k) => k === 'view').length).toBe(1);
    expect(kinds.filter((k) => k === 'card_open').length).toBe(1);
  });

  it('emits an action analytics event on a contact-link click (§5.9)', () => {
    const fixture = render();
    landed(
      fixture,
      details({ contactLinks: [{ kind: 'site', url: 'https://venue.example', label: 'Сайт' }] }),
    );
    (el(fixture, 'contact-link') as HTMLAnchorElement).click();
    TestBed.inject(AnalyticsEmitterService).flush();
    const actions = ingestBatches.flat().filter((e) => e.kind === 'action');
    expect(actions.length).toBe(1);
    expect(actions[0].restaurantId).toBe('r-1');
  });

  it('resolves the dish name from the catalog when the taxonomy is loaded', () => {
    const fixture = render();
    landed(
      fixture,
      details({
        menuItems: [{ id: 'm-1', dishId: 'd-1', priceAmount: 120, priceCurrency: 'UAH' }],
      }),
    );
    expect((el(fixture, 'menu-item-name')?.textContent ?? '').trim()).toBe('Борщ');
  });

  it('renders the neutral dish-name fallback when the catalog cannot resolve the dish', () => {
    const fixture = render();
    landed(
      fixture,
      details({
        menuItems: [{ id: 'm-1', dishId: 'unknown-dish', priceAmount: 50, priceCurrency: 'UAH' }],
      }),
    );
    // The stub catalog returns undefined for an unknown dish → the neutral fallback label is shown.
    expect((el(fixture, 'menu-item-name')?.textContent ?? '').trim()).toBe('Страва');
  });
});
