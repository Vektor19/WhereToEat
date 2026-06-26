import { Routes } from '@angular/router';
import { adminGuard } from 'core';

/**
 * Operator portal surface (Surface B) routes — lazy-loaded and admin-gated (Step 18).
 *
 * The **entire** portal subtree is a single lazy chunk gated by {@link adminGuard}: an admin/operator
 * session is admitted, a non-admin session is denied, and an anonymous caller is sent through the
 * Authorization Code + PKCE login (see {@link adminGuard}). The Admin host enforces the same policy
 * server-side, so the guard is a UX gate, not the security boundary.
 *
 * The guard is placed on the parent (shell) route so it runs **once** for the whole subtree; every
 * child feature added by Steps 19/20/24 inherits the gate without re-declaring it. The
 * {@link PortalShellComponent} provides the operator chrome (top bar / side nav / content outlet);
 * feature routes render into its `<router-outlet>`.
 *
 * **Persona decision (design Risk #3).** The portal targets the **admin/operator** persona on the
 * Admin host; restaurant-owner self-serve / claim-verify is a future backend dependency. The nav is a
 * data-driven list and the gate is a single role check, so an owner persona (an additional role + an
 * owner-scoped nav subset) can be added later without restructuring this tree.
 *
 * Portal code MUST NOT import consumer internals, and may only reach `core` through its public-api
 * barrel — both enforced by the ESLint import-boundary rules.
 */
export const portalRoutes: Routes = [
  {
    path: '',
    canActivate: [adminGuard],
    canActivateChild: [adminGuard],
    loadComponent: () => import('./portal-shell.component').then((m) => m.PortalShellComponent),
    children: [
      {
        path: '',
        // Landing/dashboard placeholder the guard protects. Steps 20/24 register their remaining
        // management features as siblings here (verified / ads / analytics).
        loadComponent: () => import('./portal-home.component').then((m) => m.PortalHomeComponent),
      },
      // ── Step 19 management features ──────────────────────────────────────────────────────────
      // Each is its own lazy chunk (`loadChildren`) inheriting the parent's `adminGuard`; the
      // data-driven `PORTAL_NAV` already links to these paths.
      {
        path: 'menu',
        loadChildren: () => import('./menu/menu.routes').then((m) => m.menuRoutes),
      },
      {
        path: 'protection',
        loadChildren: () =>
          import('./protection/protection.routes').then((m) => m.protectionRoutes),
      },
      {
        path: 'address',
        loadChildren: () => import('./address/address.routes').then((m) => m.addressRoutes),
      },
      {
        path: 'photos',
        loadChildren: () => import('./photos/photos.routes').then((m) => m.photosRoutes),
      },
      // ── Step 20 monetization features ────────────────────────────────────────────────────────
      // Verified subscription (grant Basic/Pro + revoke) and labeled ad placements — each its own
      // lazy chunk inheriting the parent's `adminGuard`; the data-driven `PORTAL_NAV` links to these.
      {
        path: 'verified',
        loadChildren: () => import('./verified/verified.routes').then((m) => m.verifiedRoutes),
      },
      {
        path: 'ads',
        loadChildren: () => import('./ads/ads.routes').then((m) => m.adsRoutes),
      },
      // ── Step 24 analytics dashboard ──────────────────────────────────────────────────────────
      // The §7.5 operator dashboard — its own lazy chunk inheriting the parent's `adminGuard`, wired
      // to the admin-host aggregates-only analytics reads (invariant #11). `PORTAL_NAV` links here.
      {
        path: 'analytics',
        loadChildren: () => import('./analytics/analytics.routes').then((m) => m.analyticsRoutes),
      },
    ],
  },
];
