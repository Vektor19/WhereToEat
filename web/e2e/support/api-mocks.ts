import type { Page, Route } from '@playwright/test';

/**
 * Deterministic Playwright route-mocks for the consumer happy-path journey.
 *
 * The SPA always addresses the backend through the single gateway origin with relative
 * `/api/public/...` (and `/api/admin/...`) paths (see `ApiConfig`). These mocks intercept
 * those relative requests so the **consumer UI journey can be exercised end-to-end without a
 * live backend, DB, or Keycloak** — the journey under test is the SPA's behaviour (build a
 * selection → recommend → results → view on map → details), not the backend's, and mocking the
 * gateway keeps that journey fast and deterministic in any environment (CI included).
 *
 * The fixture data mirrors the backend wire shapes (camelCase) that the `core` domain models
 * declare, so the SPA parses them exactly as it would a real response. Order is significant in
 * the recommend response (invariant #5 — explicit sort dominates, the client never re-sorts);
 * the fixture is deliberately NOT in name/price order so the e2e proves the UI preserves
 * backend order.
 */

/** Categories returned by `GET /categories`. */
const CATEGORIES = [
  { id: 'c-1', name: 'Перші страви' },
  { id: 'c-2', name: 'Фаст-фуд' },
];

/** Dishes returned by `GET /categories/c-1/dishes`. */
const DISHES_C1 = [
  { id: 'd-1', categoryId: 'c-1', canonicalName: 'Борщ' },
  { id: 'd-2', categoryId: 'c-1', canonicalName: 'Солянка' },
];

/**
 * Recommend result — intentionally anti-alphabetic / anti-price so the test proves the UI keeps
 * the backend order (invariant #5). `bravo` is cheaper but listed second.
 */
const RECOMMEND_RESULT = {
  match: 'or',
  sort: 'price',
  restaurants: [
    {
      restaurantId: 'r-alpha',
      name: 'Альфа Кафе',
      basketPriceAmount: 180,
      basketPriceCurrency: 'UAH',
      smoothedRating: 4.6,
      ratingCount: 128,
      distanceKm: null,
      coverage: 1,
    },
    {
      restaurantId: 'r-bravo',
      name: 'Браво Бістро',
      basketPriceAmount: 120,
      basketPriceCurrency: 'UAH',
      smoothedRating: 4.2,
      ratingCount: 64,
      distanceKm: null,
      coverage: 1,
    },
  ],
};

/** Map payload for `GET /map/r-alpha` — degrades to a deep-link when no Embed credential is set. */
const MAP_PAYLOAD = {
  restaurantId: 'r-alpha',
  hasMapData: true,
  latitude: null,
  longitude: null,
  placeId: 'ChIJ-test-place',
  mapsDeepLink: 'https://maps.google.com/?q=place_id:ChIJ-test-place',
};

/** Restaurant details for `GET /restaurants/r-alpha`. Contact links are always present (§5.8). */
const DETAILS = {
  id: 'r-alpha',
  name: 'Альфа Кафе',
  addressLine: 'вул. Хрещатик, 1',
  addressCity: 'Київ',
  contactLinks: [
    { kind: 'site', url: 'https://alpha.example', label: 'Сайт' },
    { kind: 'phone', url: 'tel:+380441234567', label: '+380 44 123 45 67' },
  ],
  menuItems: [
    { id: 'm-1', dishId: 'd-1', priceAmount: 180, priceCurrency: 'UAH', weight: '350 г' },
  ],
};

function json(route: Route, body: unknown, status = 200): Promise<void> {
  return route.fulfill({
    status,
    contentType: 'application/json',
    body: JSON.stringify(body),
  });
}

/**
 * Install the consumer-journey gateway mocks on a page. Matching is by URL substring on the
 * relative `/api/public/...` path so it is resilient to the (empty) gateway origin.
 */
export async function installConsumerApiMocks(page: Page): Promise<void> {
  // Playwright matches routes in REVERSE registration order (the last-registered matching handler
  // wins), so the broad catch-all is registered FIRST and the specific endpoint mocks AFTER it —
  // that way each specific path overrides the fallback.

  // Fallback: any other public call the SPA happens to issue (e.g. `/health`) gets a benign
  // response so an unmocked request never hangs the journey.
  await page.route('**/api/public/**', (route) => {
    if (route.request().method() === 'GET') {
      return json(route, {});
    }
    return route.fulfill({ status: 204 });
  });

  // Analytics ingest is best-effort/non-blocking — acknowledge it so the journey is not slowed
  // by an unmatched request, but assert nothing about it here (it is unit-covered in Step 16).
  await page.route('**/api/public/analytics/events', (route) => route.fulfill({ status: 202 }));

  await page.route('**/api/public/categories/c-1/dishes', (route) => json(route, DISHES_C1));
  await page.route('**/api/public/categories', (route) => json(route, CATEGORIES));
  await page.route('**/api/public/recommend', (route) => json(route, RECOMMEND_RESULT));
  await page.route('**/api/public/map/r-alpha', (route) => json(route, MAP_PAYLOAD));
  await page.route('**/api/public/restaurants/r-alpha', (route) => json(route, DETAILS));
}

export const MOCK_FIXTURES = {
  CATEGORIES,
  DISHES_C1,
  RECOMMEND_RESULT,
  MAP_PAYLOAD,
  DETAILS,
};
