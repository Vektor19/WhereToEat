import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    loadChildren: () => import('./consumer/consumer.routes').then((m) => m.consumerRoutes),
  },
  {
    path: 'portal',
    loadChildren: () => import('./portal/portal.routes').then((m) => m.portalRoutes),
  },
];
