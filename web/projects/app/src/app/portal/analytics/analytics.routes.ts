import { Routes } from '@angular/router';

/**
 * Operator §7.5 analytics-dashboard feature routes (Step 24) — its own lazy chunk under the portal
 * subtree, inheriting the parent shell's {@link adminGuard} (no re-declaration needed). The
 * data-driven `PORTAL_NAV` already links to this `analytics` path. The container is a smart component
 * that reads the admin-host, aggregates-only analytics endpoints (invariant #11).
 */
export const analyticsRoutes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./analytics-dashboard.component').then((m) => m.AnalyticsDashboardComponent),
  },
];
