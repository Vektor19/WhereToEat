import { test, expect, type Page } from '@playwright/test';

import { liveStackEnabled, liveStack, LIVE_STACK_SKIP_REASON } from './support/live-stack';

/**
 * Journey 3 — admin sign-in → a portal management action → the analytics dashboard (live stack).
 *
 * Exercises the **real** admin path: OIDC login as an admin/operator, the `adminGuard` admitting
 * the portal (a non-admin token is rejected server-side too), a bearer-authenticated admin write
 * (protection flag — admin > parser, invariant #3), and the aggregates-only analytics dashboard
 * wired to the admin read endpoint (invariant #11). Like the rating journey it needs live
 * Keycloak + the AdminApi host + DB, so it is gated behind `E2E_LIVE_STACK=1`
 * (see `support/live-stack.ts`) and skips with a descriptive reason otherwise.
 */
test.describe('admin portal management + analytics dashboard', () => {
  test.skip(!liveStackEnabled, LIVE_STACK_SKIP_REASON);

  async function keycloakSignIn(page: Page, username: string, password: string): Promise<void> {
    await page.waitForURL(/\/realms\//, { timeout: 30_000 });
    await page.fill('#username', username);
    await page.fill('#password', password);
    await page.click('#kc-login');
    await page.waitForURL((url) => url.hostname === 'localhost', { timeout: 30_000 });
  }

  test('admin signs in, toggles a protection flag, and views the analytics dashboard', async ({
    page,
  }) => {
    // Reaching the portal triggers the admin login (the `adminGuard` requires the admin role).
    await page.goto('/portal');
    await page.getByTestId('portal-login').click();
    await keycloakSignIn(page, liveStack.adminUsername, liveStack.adminPassword);

    // The portal shell is admitted for an admin token (not the `portal-denied` state).
    await page.goto('/portal');
    await expect(page.getByTestId('portal-denied')).toHaveCount(0);
    await expect(page.getByTestId('portal-role')).toContainText(/admin/i);

    // ── A management action: toggle a per-item do-not-parse protection flag (invariant #3) ──
    await page.getByTestId('portal-nav-protection').click();
    await expect(page.getByTestId('protection-flags')).toBeVisible();
    await page.getByTestId('restaurant-id-input').fill(liveStack.restaurantId);
    await page.getByTestId('restaurant-load').click();

    // The restaurant's do-not-update toggle drives `PUT /admin/restaurants/{id}/do-not-update`.
    await page.getByTestId('restaurant-do-not-update-toggle').click();
    // A 204 clears the error strip; assert no error surfaced for the write.
    await expect(page.getByTestId('restaurant-flag-error')).toHaveCount(0);

    // ── The analytics dashboard — aggregates only (invariant #11) ──
    await page.getByTestId('portal-nav-analytics').click();
    await expect(page.getByTestId('analytics-dashboard')).toBeVisible();
    await page.getByTestId('analytics-restaurant-input').fill(liveStack.restaurantId);
    await page.getByTestId('analytics-load').click();

    // At least the traffic family renders from the admin read endpoint.
    await expect(page.getByTestId('analytics-traffic')).toBeVisible({ timeout: 15_000 });
    // The aggregates-only privacy note is present end-to-end.
    await expect(page.getByTestId('analytics-privacy')).toBeVisible();
  });
});
