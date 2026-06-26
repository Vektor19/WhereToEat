import { Routes } from '@angular/router';

/**
 * Labeled ad-placement feature route (Step 20) — its own lazy chunk under the admin-gated portal
 * subtree. Inherits the {@link adminGuard} from the parent portal route. The data-driven nav's `ads`
 * entry resolves here.
 */
export const adsRoutes: Routes = [
  {
    path: '',
    loadComponent: () => import('./ad-placement.component').then((m) => m.AdPlacementComponent),
  },
];
