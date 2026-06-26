import { Routes } from '@angular/router';

/**
 * Address-management feature route (Step 19) — its own lazy chunk under the admin-gated portal subtree.
 * Inherits the {@link adminGuard} from the parent portal route. The data-driven nav's `address` entry
 * resolves here.
 */
export const addressRoutes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./address-management.component').then((m) => m.AddressManagementComponent),
  },
];
