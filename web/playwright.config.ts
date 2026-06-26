import { defineConfig, devices } from '@playwright/test';

/**
 * Playwright e2e configuration.
 *
 * The `e2e/` suite covers the three key journeys (consumer happy path, rating
 * submit, admin portal + analytics dashboard) plus axe a11y scans. The consumer
 * and a11y specs run deterministically against route-mocks; the two authenticated
 * journeys are gated behind `E2E_LIVE_STACK=1` and skip otherwise (see
 * `e2e/support/live-stack.ts`). Browsers are not bundled — run
 * `npx playwright install chromium` first.
 *
 * `webServer` boots the Angular dev server (which also wires the `/api` proxy)
 * so specs drive the running SPA.
 */
export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  forbidOnly: !!process.env['CI'],
  retries: process.env['CI'] ? 2 : 0,
  reporter: 'list',
  use: {
    baseURL: 'http://localhost:4200',
    trace: 'on-first-retry',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
  webServer: {
    command: 'npm run start',
    url: 'http://localhost:4200',
    reuseExistingServer: !process.env['CI'],
    timeout: 120_000,
  },
});
