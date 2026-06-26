# Web

This project was generated using [Angular CLI](https://github.com/angular/angular-cli) version 21.2.16.

## Development server

To start a local development server, run:

```bash
ng serve
```

Once the server is running, open your browser and navigate to `http://localhost:4200/`. The application will automatically reload whenever you modify any of the source files.

## Code scaffolding

Angular CLI includes powerful code scaffolding tools. To generate a new component, run:

```bash
ng generate component component-name
```

For a complete list of available schematics (such as `components`, `directives`, or `pipes`), run:

```bash
ng generate --help
```

## Building

To build the project run:

```bash
ng build
```

This will compile your project and store the build artifacts in the `dist/` directory. By default, the production build optimizes your application for performance and speed.

## Running unit tests

Unit tests run on the [Vitest](https://vitest.dev/) runner (via the Angular `unit-test` builder):

```bash
npm run test            # both projects, no coverage
npm run test:coverage   # both projects WITH the coverage gate (CI gates on this)
```

`test:coverage` enforces minimum coverage thresholds configured per project in `angular.json`
(the `coverage` test configuration). The `core` library — the portable business logic that is the
blueprint for the future React Native rewrite — is held to the higher bar (90% statements / 85%
branches / 95% functions / 90% lines); the `app` surface is held to 80/75/70/80. A run that drops
below a threshold exits non-zero so CI fails the build.

## Running end-to-end tests

E2e tests run on [Playwright](https://playwright.dev/). Install the browser once, then run the
suite (Playwright boots the dev server, which also wires the `/api` proxy):

```bash
npx playwright install chromium
npm run e2e
```

The suite covers the three key journeys:

1. **Consumer happy path** (`consumer-journey.spec.ts`) — build a selection → match/sort/filters →
   recommend → results → view on map → details. It is **anonymous** and runs deterministically
   against Playwright route-mocks (`e2e/support/api-mocks.ts`), so it needs no backend, DB, or
   Keycloak and runs in CI.
2. **Rating submit** (`rating-journey.spec.ts`) — sign in (OIDC) → submit a 1..5 score →
   `rating_given` emitted.
3. **Admin portal + analytics dashboard** (`portal-journey.spec.ts`) — admin sign-in → a management
   action → the analytics dashboard.

`a11y.spec.ts` runs automated [axe-core](https://github.com/dequelabs/axe-core) WCAG scans over the
key consumer screens (selection builder, results list, map modal, details), asserting zero
critical/serious violations.

Journeys **2 and 3 require the live stack** (the `deploy/` docker-compose: PublicApi `:8080` +
AdminApi `:8081` behind the gateway + a SQL DB + Keycloak). They are gated behind `E2E_LIVE_STACK=1`
plus the Keycloak/user/admin/restaurant env vars (see `e2e/support/live-stack.ts`); when those are
unset the specs **skip with a descriptive reason** rather than fail. To run them against the live
stack:

```bash
E2E_LIVE_STACK=1 \
  E2E_USER_USERNAME=... E2E_USER_PASSWORD=... \
  E2E_ADMIN_USERNAME=... E2E_ADMIN_PASSWORD=... \
  E2E_RESTAURANT_ID=<seeded-guid> \
  npm run e2e
```

## Additional Resources

For more information on using the Angular CLI, including detailed command references, visit the [Angular CLI Overview and Command Reference](https://angular.dev/tools/cli) page.
