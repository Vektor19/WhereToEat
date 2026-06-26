import { test, expect, type Page } from '@playwright/test';

import { liveStackEnabled, liveStack, LIVE_STACK_SKIP_REASON } from './support/live-stack';

/**
 * Journey 2 — sign in → submit a rating → `rating_given` emitted (live stack).
 *
 * This journey exercises the **real** authenticated path: OIDC Authorization Code + PKCE against
 * Keycloak, a bearer-authenticated `POST /restaurants/{id}/ratings`, the smoothed-label honesty
 * copy (invariant #6), and the `rating_given` analytics event flushed to the ingest endpoint.
 * Because it depends on live Keycloak + the PublicApi host + DB, it is gated behind
 * `E2E_LIVE_STACK=1` (see `support/live-stack.ts`); without the stack configured it skips with a
 * descriptive reason rather than faking a session (which would not cover the redirect/PKCE flow
 * this spec exists for).
 */
test.describe('rating submit (authenticated)', () => {
  test.skip(!liveStackEnabled, LIVE_STACK_SKIP_REASON);

  /**
   * Drive the Keycloak login form that `AuthService.login()` redirects to. The username/password
   * come from env (never hardcoded). Field selectors are Keycloak's standard login template ids.
   */
  async function keycloakSignIn(page: Page, username: string, password: string): Promise<void> {
    await page.waitForURL(/\/realms\//, { timeout: 30_000 });
    await page.fill('#username', username);
    await page.fill('#password', password);
    await page.click('#kc-login');
    // Back on the SPA after the Authorization Code callback is processed.
    await page.waitForURL((url) => url.port === '4200' || url.hostname === 'localhost', {
      timeout: 30_000,
    });
  }

  test('signs in, submits a 1..5 score, and emits rating_given', async ({ page }) => {
    // Capture the analytics ingest batch so we can assert a `rating_given` event was flushed.
    const ratingGivenSeen = page.waitForRequest((req) => {
      if (!req.url().includes('/api/public/analytics/events') || req.method() !== 'POST') {
        return false;
      }
      const body = req.postData() ?? '';
      // The wire `Kind` is the enum-member spelling the parser accepts (`RatingGiven`).
      return /ratinggiven/i.test(body);
    });

    await page.goto(`/restaurant/${liveStack.restaurantId}`);

    // Unauthenticated: the rating widget shows the sign-in gate, not the star input.
    await expect(page.getByTestId('rating-signin')).toBeVisible();
    await page.getByTestId('rating-signin-button').click();

    await keycloakSignIn(page, liveStack.userUsername, liveStack.userPassword);

    // Authenticated: the star input is now available and the smoothed-label honesty note is shown.
    await page.goto(`/restaurant/${liveStack.restaurantId}`);
    await expect(page.getByTestId('rating-stars')).toBeVisible();
    await expect(page.getByTestId('rating-smoothed-note')).toBeVisible();

    // Pick a score (1..5) and submit.
    await page.locator('[data-score="4"]').click();
    await page.getByTestId('rating-submit-button').click();

    // The submit succeeded (204) and a `rating_given` analytics event was flushed.
    await expect(page.getByTestId('rating-success')).toBeVisible({ timeout: 15_000 });
    await ratingGivenSeen;
  });
});
