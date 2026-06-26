import { Routes } from '@angular/router';

/**
 * Photo-permission feature route (Step 19) — its own lazy chunk under the admin-gated portal subtree.
 * Inherits the {@link adminGuard} from the parent portal route. The data-driven nav's `photos` entry
 * resolves here.
 */
export const photosRoutes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./photo-permission.component').then((m) => m.PhotoPermissionComponent),
  },
];
