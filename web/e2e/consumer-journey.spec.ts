import { test, expect } from '@playwright/test';

import { installConsumerApiMocks, MOCK_FIXTURES } from './support/api-mocks';

/**
 * Journey 1 — consumer happy path (anonymous, deterministic).
 *
 * Build a selection (browse the two-level taxonomy) → choose match / sort / filters → recommend
 * → results list (in backend order) → "View on map" inline → restaurant details (always-free
 * contact links). The whole journey is **anonymous** (public reads need no token), so it runs
 * with route-mocked gateway responses and needs no backend, DB, or Keycloak — it can run in any
 * environment, including CI. It exercises the SPA's behaviour, which is what the e2e is for.
 *
 * Invariants asserted along the way: deterministic selection search (#1/#2), explicit sort
 * dominates / coverage secondary (#5), our smoothed rating (#6), always-free contact links
 * (§5.8/#10), no per-item "≈"/"updated X days ago" disclaimer on the card (#12/§10).
 */
test.describe('consumer happy path', () => {
  test.beforeEach(async ({ page }) => {
    await installConsumerApiMocks(page);
  });

  test('build selection → recommend → results → view on map → details', async ({ page }) => {
    await page.goto('/');

    // ── Build a selection by browsing the two-level taxonomy (invariant #1 & #2) ──
    // The browse tab is the default surface; open a category to reveal its dishes, then add one.
    await page.getByTestId('category-c-1').click();
    await expect(page.getByTestId('dish-list')).toBeVisible();
    await page.getByTestId('dish-d-1').click();

    // The selection chip reflects the added dish.
    await expect(page.getByTestId('selection-summary')).toContainText('Борщ');

    // ── Choose match / sort / filters (explicit-sort-dominates UX, invariant #5) ──
    await page.getByTestId('match-or').click();
    await page.getByTestId('sort-price').click();
    await page.getByTestId('filter-input-price').fill('500');

    // ── Recommend ──
    await page.getByTestId('query-submit').click();

    // ── Results list renders in BACKEND order (invariant #5 — client never re-sorts) ──
    await expect(page.getByTestId('results-list')).toBeVisible();
    const names = await page.getByTestId('result-name').allTextContents();
    expect(names).toEqual([
      MOCK_FIXTURES.RECOMMEND_RESULT.restaurants[0].name,
      MOCK_FIXTURES.RECOMMEND_RESULT.restaurants[1].name,
    ]);

    // No per-item price disclaimer / "≈" / "updated X days ago" on a card (invariant #12 / §10).
    const firstCard = page.getByTestId('result-card-r-alpha');
    await expect(firstCard).not.toContainText('≈');
    await expect(firstCard).not.toContainText('оновлено');

    // Our smoothed rating is shown (invariant #6).
    await expect(firstCard.getByTestId('result-rating')).toBeVisible();

    // ── Inline "View on map" — opens the live map without navigating away (§5.7) ──
    await firstCard.getByTestId('result-view-map').click();
    await expect(page.getByTestId('map-dialog')).toBeVisible();
    // With no Embed credential configured the provider degrades to a Maps deep-link (decision #11).
    await expect(page.getByTestId('map-embed').or(page.getByTestId('map-deeplink'))).toBeVisible();
    await page.getByTestId('map-close').click();
    await expect(page.getByTestId('map-dialog')).toHaveCount(0);
    // Still on the results list — the map did not navigate away.
    await expect(page.getByTestId('results-list')).toBeVisible();

    // ── Restaurant details — always-free contact links (§5.8, invariant #10) ──
    await firstCard.getByTestId('result-open').click();
    await expect(page).toHaveURL(/\/restaurant\/r-alpha$/);
    await expect(page.getByTestId('details-name')).toContainText('Альфа Кафе');
    await expect(page.getByTestId('contact-links')).toBeVisible();
    // Both the site and phone links are present and free (no paywall / no Verified gate).
    await expect(page.getByTestId('contact-link')).toHaveCount(
      MOCK_FIXTURES.DETAILS.contactLinks.length,
    );
    await expect(page.getByTestId('menu-list')).toBeVisible();
  });
});
