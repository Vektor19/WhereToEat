import AxeBuilder from '@axe-core/playwright';
import { test, expect } from '@playwright/test';

import { installConsumerApiMocks } from './support/api-mocks';

/**
 * Automated accessibility (axe) scans on the key consumer screens.
 *
 * This is the home for the axe scans deferred from the Step 25 hardening pass: axe-core could not
 * live in the unit-test (jsdom) layer, so it runs here in the real browser over the key screens of
 * the deterministic (mocked) consumer journey — selection builder, results list, map modal, and
 * restaurant details. The journeys for the authenticated rating/portal screens are live-stack
 * gated (see the other specs); the high-traffic anonymous screens are the ones with the most
 * a11y surface and they are covered here with no infra dependency.
 *
 * We assert **zero critical/serious WCAG violations** (Step 25 acceptance: "no critical a11y
 * violations in an automated audit"). Lesser-severity findings are not failed here to keep the
 * gate meaningful and stable; they remain visible in the run output.
 */
const BLOCKING_IMPACTS = ['critical', 'serious'];

function blockingViolations(results: { violations: { impact?: string | null }[] }): unknown[] {
  return results.violations.filter((v) => BLOCKING_IMPACTS.includes(v.impact ?? ''));
}

test.describe('a11y — key consumer screens (axe)', () => {
  test.beforeEach(async ({ page }) => {
    await installConsumerApiMocks(page);
  });

  test('selection builder has no critical/serious violations', async ({ page }) => {
    await page.goto('/');
    await expect(page.getByTestId('category-list')).toBeVisible();

    const results = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa']).analyze();
    expect(blockingViolations(results)).toEqual([]);
  });

  test('results list and map modal have no critical/serious violations', async ({ page }) => {
    await page.goto('/');
    await page.getByTestId('category-c-1').click();
    await page.getByTestId('dish-d-1').click();
    await page.getByTestId('query-submit').click();
    await expect(page.getByTestId('results-list')).toBeVisible();

    const listResults = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa']).analyze();
    expect(blockingViolations(listResults)).toEqual([]);

    // The map modal is a focus-trapping dialog — scan it open.
    await page.getByTestId('result-card-r-alpha').getByTestId('result-view-map').click();
    await expect(page.getByTestId('map-dialog')).toBeVisible();
    const modalResults = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa']).analyze();
    expect(blockingViolations(modalResults)).toEqual([]);
  });

  test('restaurant details has no critical/serious violations', async ({ page }) => {
    await page.goto('/restaurant/r-alpha');
    await expect(page.getByTestId('restaurant-details')).toBeVisible();

    const results = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa']).analyze();
    expect(blockingViolations(results)).toEqual([]);
  });
});
