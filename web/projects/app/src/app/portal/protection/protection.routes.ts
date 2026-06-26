import { Routes } from '@angular/router';

/**
 * Protection-flags feature route (Step 19) — its own lazy chunk under the admin-gated portal subtree.
 * Inherits the {@link adminGuard} from the parent portal route. The data-driven nav's `protection`
 * entry resolves here.
 */
export const protectionRoutes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./protection-flags.component').then((m) => m.ProtectionFlagsComponent),
  },
];
