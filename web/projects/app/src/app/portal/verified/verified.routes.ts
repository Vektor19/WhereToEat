import { Routes } from '@angular/router';

/**
 * Verified-subscription feature route (Step 20) — its own lazy chunk under the admin-gated portal
 * subtree. Inherits the {@link adminGuard} from the parent portal route. The data-driven nav's
 * `verified` entry resolves here.
 */
export const verifiedRoutes: Routes = [
  {
    path: '',
    loadComponent: () => import('./verified.component').then((m) => m.VerifiedComponent),
  },
];
