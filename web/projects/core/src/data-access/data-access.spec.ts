import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
  type TestRequest,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';

import { ApiConfig } from '../config/api-config';
import { APP_CONFIG, type AppConfig } from '../config/app-config.token';
import type {
  CategoryDto,
  ConversionFunnelDto,
  DemandBreakdownDto,
  DishDto,
  IdentityDto,
  MapPayloadDto,
  PricePositioningDto,
  RatingsDistributionDto,
  RawEventBody,
  RecommendationRequest,
  RecommendationResultDto,
  RestaurantDetailsDto,
  RestaurantTrafficSummaryDto,
} from '../domain';
import { EVENT_KIND_WIRE_NAME } from '../domain';

import type { VerifiedTier } from './admin/admin-bodies';
import {
  GET_CATEGORIES,
  GET_DISHES_BY_CATEGORY,
  GET_RESTAURANT_DETAILS,
  SEARCH_CATEGORIES,
  SEARCH_DISHES,
  RECOMMEND,
  GET_MAP,
  INGEST_ANALYTICS,
  GET_ME,
  SUBMIT_RATING,
  GET_HEALTH,
  EDIT_MENU_ITEM,
  EDIT_ADDRESS,
  SET_MENU_ITEM_DO_NOT_PARSE,
  SET_RESTAURANT_DO_NOT_UPDATE,
  SET_PHOTO_PERMISSION,
  GRANT_VERIFIED,
  REVOKE_VERIFIED,
  CREATE_AD_PLACEMENT,
  GET_TRAFFIC,
  GET_FUNNEL,
  GET_DEMAND,
  GET_PRICE_POSITIONING,
  GET_RATINGS_DISTRIBUTION,
} from './tokens';

function appConfig(): AppConfig {
  return {
    production: false,
    gatewayOrigin: '',
    apiPublicPrefix: '/api/public',
    apiAdminPrefix: '/api/admin',
    auth: { issuer: '', clientId: 'spa', redirectUri: '', scope: 'openid' },
    map: { provider: 'google-embed', googleMapsEmbedKey: '' },
    features: { analyticsEnabled: true, ratingSubmitEnabled: true, portalEnabled: true },
  };
}

function setup(): { http: HttpTestingController } {
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      ApiConfig,
      { provide: APP_CONFIG, useValue: appConfig() },
    ],
  });
  return { http: TestBed.inject(HttpTestingController) };
}

/** Drains a one-shot HTTP call: invokes it, asserts a single request, and flushes the response. */
async function flush<T>(
  http: HttpTestingController,
  call: Promise<T>,
  expect_: { method: string; url: string },
  body: string | number | boolean | object | null,
  opts?: { status?: number; statusText?: string },
): Promise<{ value: T; req: TestRequest }> {
  const req = http.expectOne(
    (r) => r.method === expect_.method && r.urlWithParams === expect_.url,
    `${expect_.method} ${expect_.url}`,
  );
  req.flush(body, { status: opts?.status ?? 200, statusText: opts?.statusText ?? 'OK' });
  const value = await call;
  return { value, req };
}

describe('data-access — public operations', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    http = setup().http;
  });

  afterEach(() => {
    http.verify();
    TestBed.resetTestingModule();
  });

  it('getCategories: GET the public categories list', async () => {
    const op = TestBed.inject(GET_CATEGORIES);
    const expected: CategoryDto[] = [{ id: 'c1', name: 'Перші страви' }];

    const { value } = await flush(
      http,
      firstValueFrom(op.execute()),
      {
        method: 'GET',
        url: '/api/public/categories',
      },
      expected,
    );

    expect(value).toEqual(expected);
  });

  it('getDishesByCategory: GET dishes under the category id', async () => {
    const op = TestBed.inject(GET_DISHES_BY_CATEGORY);
    const expected: DishDto[] = [{ id: 'd1', categoryId: 'c1', canonicalName: 'Борщ' }];

    const { value } = await flush(
      http,
      firstValueFrom(op.execute('c1')),
      {
        method: 'GET',
        url: '/api/public/categories/c1/dishes',
      },
      expected,
    );

    expect(value).toEqual(expected);
  });

  it('getRestaurantDetails: GET the restaurant by id', async () => {
    const op = TestBed.inject(GET_RESTAURANT_DETAILS);
    const expected: RestaurantDetailsDto = {
      id: 'r1',
      name: 'Смак',
      addressLine: 'вул. Хрещатик, 1',
      addressCity: 'Київ',
      contactLinks: [],
      menuItems: [],
    };

    const { value } = await flush(
      http,
      firstValueFrom(op.execute('r1')),
      {
        method: 'GET',
        url: '/api/public/restaurants/r1',
      },
      expected,
    );

    expect(value).toEqual(expected);
  });

  it('getRestaurantDetails: a 404 propagates as an error (Step 7 normalizes it)', async () => {
    const op = TestBed.inject(GET_RESTAURANT_DETAILS);
    const call = firstValueFrom(op.execute('missing'));

    const req = http.expectOne('/api/public/restaurants/missing');
    req.flush(
      { error: 'Catalog.NotFound', message: 'Restaurant not found.' },
      { status: 404, statusText: 'Not Found' },
    );

    await expect(call).rejects.toMatchObject({ status: 404 });
  });

  it('searchCategories: GET with prefix and limit query params', async () => {
    const op = TestBed.inject(SEARCH_CATEGORIES);
    const expected: CategoryDto[] = [{ id: 'c1', name: 'Фаст-фуд' }];

    const { value, req } = await flush(
      http,
      firstValueFrom(op.execute('фа', 5)),
      {
        method: 'GET',
        url: '/api/public/search/categories?prefix=%D1%84%D0%B0&limit=5',
      },
      expected,
    );

    expect(req.request.params.get('prefix')).toBe('фа');
    expect(req.request.params.get('limit')).toBe('5');
    expect(value).toEqual(expected);
  });

  it('searchDishes: GET with prefix and limit query params', async () => {
    const op = TestBed.inject(SEARCH_DISHES);
    const expected: DishDto[] = [{ id: 'd1', categoryId: 'c1', canonicalName: 'Піца Маргарита' }];

    const { req } = await flush(
      http,
      firstValueFrom(op.execute('піц', 10)),
      {
        method: 'GET',
        url: '/api/public/search/dishes?prefix=%D0%BF%D1%96%D1%86&limit=10',
      },
      expected,
    );

    expect(req.request.params.get('prefix')).toBe('піц');
    expect(req.request.params.get('limit')).toBe('10');
  });

  it('recommend: POST the exact wire body for a mixed selection with geo', async () => {
    const op = TestBed.inject(RECOMMEND);
    const request: RecommendationRequest = {
      items: [{ categoryId: 'c1' }, { dishId: 'd1' }],
      match: 'or',
      sort: 'price',
      filters: [{ key: 'price', value: '150' }, { key: 'rating' }],
      userGeo: { latitude: 50.45, longitude: 30.52 },
    };
    const result: RecommendationResultDto = { match: 'or', sort: 'price', restaurants: [] };

    const { req, value } = await flush(
      http,
      firstValueFrom(op.execute(request)),
      {
        method: 'POST',
        url: '/api/public/recommend',
      },
      result,
    );

    expect(req.request.body).toEqual({
      items: [{ categoryId: 'c1' }, { dishId: 'd1' }],
      match: 'or',
      sort: 'price',
      filters: [
        { key: 'price', value: '150' },
        { key: 'rating', value: null },
      ],
      userGeo: { latitude: 50.45, longitude: 30.52 },
    });
    expect(value).toEqual(result);
  });

  it('recommend: omits userGeo entirely when none is supplied (AND mode)', async () => {
    const op = TestBed.inject(RECOMMEND);
    const request: RecommendationRequest = {
      items: [{ dishId: 'd1' }],
      match: 'and',
      sort: 'best',
      filters: [],
    };
    const result: RecommendationResultDto = { match: 'and', sort: 'best', restaurants: [] };

    const { req } = await flush(
      http,
      firstValueFrom(op.execute(request)),
      {
        method: 'POST',
        url: '/api/public/recommend',
      },
      result,
    );

    expect(req.request.body).toEqual({
      items: [{ dishId: 'd1' }],
      match: 'and',
      sort: 'best',
      filters: [],
    });
    expect(Object.prototype.hasOwnProperty.call(req.request.body, 'userGeo')).toBe(false);
  });

  it('recommend: a validation 400 propagates the {error,message} envelope', async () => {
    const op = TestBed.inject(RECOMMEND);
    const request: RecommendationRequest = {
      items: [{ dishId: 'd1' }],
      match: 'or',
      sort: 'price',
      filters: [],
    };
    const call = firstValueFrom(op.execute(request));

    const req = http.expectOne('/api/public/recommend');
    req.flush(
      { error: 'Recommend.InvalidItem', message: 'bad item' },
      { status: 400, statusText: 'Bad Request' },
    );

    await expect(call).rejects.toMatchObject({
      status: 400,
      error: { error: 'Recommend.InvalidItem', message: 'bad item' },
    });
  });

  it('getMap: GET the live map payload by restaurant id', async () => {
    const op = TestBed.inject(GET_MAP);
    const payload: MapPayloadDto = { restaurantId: 'r1', hasMapData: false };

    const { value } = await flush(
      http,
      firstValueFrom(op.execute('r1')),
      {
        method: 'GET',
        url: '/api/public/map/r1',
      },
      payload,
    );

    expect(value).toEqual(payload);
  });

  it('ingestAnalytics: POST the batch with kind serialized to the wire spelling, expecting 202', async () => {
    const op = TestBed.inject(INGEST_ANALYTICS);
    const events: RawEventBody[] = [
      { kind: 'card_open', restaurantId: 'r1', sessionId: 's1' },
      { kind: 'rating_given', restaurantId: 'r1' },
      { kind: 'impression', position: 0 },
    ];

    const { req } = await flush(
      http,
      firstValueFrom(op.execute(events)),
      {
        method: 'POST',
        url: '/api/public/analytics/events',
      },
      null,
      { status: 202, statusText: 'Accepted' },
    );

    const body = req.request.body as { events: { kind: string }[] };
    expect(body.events.map((e) => e.kind)).toEqual([
      EVENT_KIND_WIRE_NAME['card_open'],
      EVENT_KIND_WIRE_NAME['rating_given'],
      EVENT_KIND_WIRE_NAME['impression'],
    ]);
    // No snake_case kind leaks onto the wire (would 400 the whole batch).
    expect(body.events.some((e) => e.kind.includes('_'))).toBe(false);
    // The rest of each event is preserved (e.g. restaurantId).
    expect(req.request.body.events[0]).toMatchObject({ kind: 'CardOpen', restaurantId: 'r1' });
  });

  it('getMe: GET the authenticated identity probe (bearer attached by Step 6)', async () => {
    const op = TestBed.inject(GET_ME);
    const me: IdentityDto = { subjectId: 'sub-1', roles: ['Admin'] };

    const { value, req } = await flush(
      http,
      firstValueFrom(op.execute()),
      {
        method: 'GET',
        url: '/api/public/me',
      },
      me,
    );

    // Step 5 attaches no bearer itself — that is the Step 6 interceptor's job.
    expect(req.request.headers.has('Authorization')).toBe(false);
    expect(value).toEqual(me);
  });

  it('submitRating: POST { score } to the nested ratings path, 204 success', async () => {
    const op = TestBed.inject(SUBMIT_RATING);

    const { req } = await flush(
      http,
      firstValueFrom(op.execute('r1', 5)),
      {
        method: 'POST',
        url: '/api/public/restaurants/r1/ratings',
      },
      null,
      { status: 204, statusText: 'No Content' },
    );

    expect(req.request.body).toEqual({ score: 5 });
    // Step 5 attaches no bearer itself — the Step 6 interceptor does (the /ratings suffix allowlist).
    expect(req.request.headers.has('Authorization')).toBe(false);
  });

  it('submitRating: a 400 {error,message} propagates as a typed failure', async () => {
    const op = TestBed.inject(SUBMIT_RATING);
    const call = firstValueFrom(op.execute('r1', 6));

    const req = http.expectOne('/api/public/restaurants/r1/ratings');
    req.flush(
      { error: 'Rating.ScoreOutOfRange', message: 'A rating score must be between 1 and 5.' },
      { status: 400, statusText: 'Bad Request' },
    );

    await expect(call).rejects.toMatchObject({
      status: 400,
      error: { error: 'Rating.ScoreOutOfRange' },
    });
  });

  it('getHealth: GET the health probe as text', async () => {
    const op = TestBed.inject(GET_HEALTH);

    // Subscribe first so the cold observable actually issues the request.
    const call = firstValueFrom(op.execute());
    const req = http.expectOne('/api/public/health');
    expect(req.request.method).toBe('GET');
    expect(req.request.responseType).toBe('text');
    req.flush('Healthy', { status: 200, statusText: 'OK' });

    expect(await call).toBe('Healthy');
  });
});

describe('data-access — admin operations', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    http = setup().http;
  });

  afterEach(() => {
    http.verify();
    TestBed.resetTestingModule();
  });

  it('editMenuItem: PUT the menu-item body to the admin host, 204 success', async () => {
    const op = TestBed.inject(EDIT_MENU_ITEM);
    const body = { dishId: 'd1', priceAmount: 150, priceCurrency: 'UAH', weight: '350 г' };

    const { req } = await flush(
      http,
      firstValueFrom(op.execute('m1', body)),
      {
        method: 'PUT',
        url: '/api/admin/admin/menu-items/m1',
      },
      null,
      { status: 204, statusText: 'No Content' },
    );

    expect(req.request.body).toEqual(body);
  });

  it('editAddress: PUT the address body to the admin host', async () => {
    const op = TestBed.inject(EDIT_ADDRESS);
    const body = { addressLine: 'вул. Хрещатик, 1', city: 'Київ' };

    const { req } = await flush(
      http,
      firstValueFrom(op.execute('r1', body)),
      {
        method: 'PUT',
        url: '/api/admin/admin/restaurants/r1/address',
      },
      null,
      { status: 204, statusText: 'No Content' },
    );

    expect(req.request.body).toEqual(body);
  });

  it('setMenuItemDoNotParse: PUT { value } to the do-not-parse endpoint', async () => {
    const op = TestBed.inject(SET_MENU_ITEM_DO_NOT_PARSE);

    const { req } = await flush(
      http,
      firstValueFrom(op.execute('m1', true)),
      {
        method: 'PUT',
        url: '/api/admin/admin/menu-items/m1/do-not-parse',
      },
      null,
      { status: 204, statusText: 'No Content' },
    );

    expect(req.request.body).toEqual({ value: true });
  });

  it('setRestaurantDoNotUpdate: PUT { value } to the do-not-update endpoint', async () => {
    const op = TestBed.inject(SET_RESTAURANT_DO_NOT_UPDATE);

    const { req } = await flush(
      http,
      firstValueFrom(op.execute('r1', false)),
      {
        method: 'PUT',
        url: '/api/admin/admin/restaurants/r1/do-not-update',
      },
      null,
      { status: 204, statusText: 'No Content' },
    );

    expect(req.request.body).toEqual({ value: false });
  });

  it('setPhotoPermission: PUT { value } to the photo permission endpoint', async () => {
    const op = TestBed.inject(SET_PHOTO_PERMISSION);

    const { req } = await flush(
      http,
      firstValueFrom(op.execute('p1', true)),
      {
        method: 'PUT',
        url: '/api/admin/admin/photos/p1/permission',
      },
      null,
      { status: 204, statusText: 'No Content' },
    );

    expect(req.request.body).toEqual({ value: true });
  });

  // The backend `SubscriptionTier` enum (None = 0, Basic = 1, Pro = 2) binds from its integer
  // value — the Admin host has no JsonStringEnumConverter — so `tier` must go on the wire as the
  // ordinal, not the member name.
  it.each<[VerifiedTier, number]>([
    ['Basic', 1],
    ['Pro', 2],
  ])('grantVerified: PUT %s sends its integer ordinal { tier: %i }', async (tier, ordinal) => {
    const op = TestBed.inject(GRANT_VERIFIED);

    const { req } = await flush(
      http,
      firstValueFrom(op.execute('v1', tier)),
      {
        method: 'PUT',
        url: '/api/admin/admin/venues/v1/verified',
      },
      null,
      { status: 204, statusText: 'No Content' },
    );

    expect(req.request.body).toEqual({ tier: ordinal });
  });

  it('revokeVerified: DELETE the verified resource', async () => {
    const op = TestBed.inject(REVOKE_VERIFIED);

    const { req } = await flush(
      http,
      firstValueFrom(op.execute('v1')),
      {
        method: 'DELETE',
        url: '/api/admin/admin/venues/v1/verified',
      },
      null,
      { status: 204, statusText: 'No Content' },
    );

    expect(req.request.body).toBeNull();
  });

  it('createAdPlacement: POST targeting+window, returns 201 { id }', async () => {
    const op = TestBed.inject(CREATE_AD_PLACEMENT);
    const body = {
      targetingKey: 'pizza:kyiv-center',
      startsAt: '2026-07-01T00:00:00Z',
      endsAt: '2026-08-01T00:00:00Z',
    };

    const { req, value } = await flush(
      http,
      firstValueFrom(op.execute('v1', body)),
      {
        method: 'POST',
        url: '/api/admin/admin/venues/v1/ad-placements',
      },
      { id: 'ad-1' },
      { status: 201, statusText: 'Created' },
    );

    expect(req.request.body).toEqual(body);
    expect(value).toEqual({ id: 'ad-1' });
  });

  it('admin write: a 400 {error,message} propagates as a typed failure', async () => {
    const op = TestBed.inject(EDIT_MENU_ITEM);
    const call = firstValueFrom(
      op.execute('m1', { dishId: 'd1', priceAmount: -1, priceCurrency: 'UAH' }),
    );

    const req = http.expectOne('/api/admin/admin/menu-items/m1');
    req.flush(
      { error: 'Admin.Validation', message: 'bad price' },
      { status: 400, statusText: 'Bad Request' },
    );

    await expect(call).rejects.toMatchObject({
      status: 400,
      error: { error: 'Admin.Validation', message: 'bad price' },
    });
  });
});

describe('data-access — admin §7.5 analytics dashboard (aggregates-only reads, Step 24)', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    http = setup().http;
  });

  afterEach(() => {
    http.verify();
    TestBed.resetTestingModule();
  });

  const from = '2026-06-01T00:00:00Z';
  const to = '2026-06-25T00:00:00Z';

  it('getTraffic: GET the admin traffic summary with the from/to window', async () => {
    const op = TestBed.inject(GET_TRAFFIC);
    const expected: RestaurantTrafficSummaryDto = {
      restaurantId: 'r1',
      impressions: 1000,
      cardOpens: 120,
      actions: 30,
      ctr: 0.12,
    };

    const { value, req } = await flush(
      http,
      firstValueFrom(op.execute('r1', from, to)),
      {
        method: 'GET',
        // Angular's HttpParams does not percent-encode the ISO colons in a query value.
        url: `/api/admin/admin/analytics/restaurants/r1/traffic?from=${from}&to=${to}`,
      },
      expected,
    );

    expect(req.request.params.get('from')).toBe(from);
    expect(req.request.params.get('to')).toBe(to);
    expect(value).toEqual(expected);
  });

  it('getFunnel: GET the admin conversion funnel with the from/to window', async () => {
    const op = TestBed.inject(GET_FUNNEL);
    const expected: ConversionFunnelDto = {
      restaurantId: 'r1',
      impressions: 1000,
      cardOpens: 120,
      actions: 30,
      impressionToCardOpenRate: 0.12,
      cardOpenToActionRate: 0.25,
    };

    const { value, req } = await flush(
      http,
      firstValueFrom(op.execute('r1', from, to)),
      {
        method: 'GET',
        url: `/api/admin/admin/analytics/restaurants/r1/funnel?from=${from}&to=${to}`,
      },
      expected,
    );

    expect(req.request.params.get('from')).toBe(from);
    expect(value).toEqual(expected);
  });

  it('getDemand: GET the area-wide demand breakdown with from/to and an optional top cap', async () => {
    const op = TestBed.inject(GET_DEMAND);
    const expected: DemandBreakdownDto = {
      categories: [{ selectionId: 'c1', searchCount: 50 }],
      dishes: [{ selectionId: 'd1', searchCount: 40 }],
    };

    const { value, req } = await flush(
      http,
      firstValueFrom(op.execute(from, to, 10)),
      {
        method: 'GET',
        url: `/api/admin/admin/analytics/demand?from=${from}&to=${to}&top=10`,
      },
      expected,
    );

    expect(req.request.params.get('top')).toBe('10');
    expect(value).toEqual(expected);
  });

  it('getDemand: omits the top param entirely when none is supplied', async () => {
    const op = TestBed.inject(GET_DEMAND);
    const expected: DemandBreakdownDto = { categories: [], dishes: [] };

    const { req } = await flush(
      http,
      firstValueFrom(op.execute(from, to)),
      {
        method: 'GET',
        url: `/api/admin/admin/analytics/demand?from=${from}&to=${to}`,
      },
      expected,
    );

    expect(req.request.params.has('top')).toBe(false);
  });

  it('getPricePositioning: GET the per-dish positioning (no window — not behavioural)', async () => {
    const op = TestBed.inject(GET_PRICE_POSITIONING);
    const expected: PricePositioningDto = {
      restaurantId: 'r1',
      dishes: [
        {
          dishId: 'd1',
          venuePrice: 150,
          medianPrice: 170,
          deltaFromMedian: -20,
          currency: 'UAH',
        },
      ],
    };

    const { value } = await flush(
      http,
      firstValueFrom(op.execute('r1')),
      {
        method: 'GET',
        url: '/api/admin/admin/analytics/restaurants/r1/price-positioning',
      },
      expected,
    );

    expect(value).toEqual(expected);
  });

  it('getRatingsDistribution: GET the cumulative all-time aggregate (no window)', async () => {
    const op = TestBed.inject(GET_RATINGS_DISTRIBUTION);
    const expected: RatingsDistributionDto = {
      restaurantId: 'r1',
      ratingCount: 42,
      scoreSum: 189,
      averageScore: 4.5,
    };

    const { value } = await flush(
      http,
      firstValueFrom(op.execute('r1')),
      {
        method: 'GET',
        url: '/api/admin/admin/analytics/restaurants/r1/ratings',
      },
      expected,
    );

    expect(value).toEqual(expected);
  });

  it('admin analytics read: a 400 {error,message} (bad window) propagates as a typed failure', async () => {
    const op = TestBed.inject(GET_TRAFFIC);
    const call = firstValueFrom(op.execute('r1', to, from));

    const req = http.expectOne(
      (r) => r.method === 'GET' && r.url === '/api/admin/admin/analytics/restaurants/r1/traffic',
    );
    req.flush(
      { error: 'Analytics.Window.Inverted', message: "'from' must not be after 'to'." },
      { status: 400, statusText: 'Bad Request' },
    );

    await expect(call).rejects.toMatchObject({
      status: 400,
      error: { error: 'Analytics.Window.Inverted' },
    });
  });

  it('admin analytics read: bearer attachment is the Step 6 interceptor job, not the op', async () => {
    const op = TestBed.inject(GET_RATINGS_DISTRIBUTION);
    const expected: RatingsDistributionDto = {
      restaurantId: 'r1',
      ratingCount: 0,
      scoreSum: 0,
      averageScore: 0,
    };

    const { req } = await flush(
      http,
      firstValueFrom(op.execute('r1')),
      {
        method: 'GET',
        url: '/api/admin/admin/analytics/restaurants/r1/ratings',
      },
      expected,
    );

    // The operation itself attaches no Authorization header — the admin-prefix branch of the bearer
    // interceptor (Step 6) does, since this rides the admin prefix.
    expect(req.request.headers.has('Authorization')).toBe(false);
  });
});
