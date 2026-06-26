import { Routes } from '@angular/router';

/**
 * Menu-management feature route (Step 19) — its own lazy chunk under the admin-gated portal subtree.
 * The parent portal route ({@link portalRoutes}) carries the {@link adminGuard}, so this child inherits
 * the gate without re-declaring it. Registered as a sibling of the portal home so the data-driven nav's
 * `menu` entry resolves here.
 */
export const menuRoutes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./menu-management.component').then((m) => m.MenuManagementComponent),
  },
];
