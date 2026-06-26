import { Routes } from '@angular/router';

/**
 * Consumer search surface (Surface A) routes — lazy-loaded.
 *
 * Step 10 registers the **selection builder** as the surface's entry point (the deterministic
 * selection flow — invariant #1 & #2). Step 14 adds the **restaurant details** route
 * (`restaurant/:id`) the results list navigates to on card-open (§5.8). Consumer code MUST NOT import
 * portal internals, and may only reach `core` through its public-api barrel (`'core'`), never via a
 * deep path — both enforced by the ESLint import-boundary rules.
 *
 * The `:id` param flows into the details container's `id` input via `withComponentInputBinding`
 * (wired in `app.config.ts`).
 */
export const consumerRoutes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./selection/selection-builder.component').then((m) => m.SelectionBuilderComponent),
  },
  {
    path: 'restaurant/:id',
    loadComponent: () =>
      import('./details/restaurant-details.component').then((m) => m.RestaurantDetailsComponent),
  },
];
