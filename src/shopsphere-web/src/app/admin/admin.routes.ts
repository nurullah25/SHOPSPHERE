import { Routes } from '@angular/router';
import { AdminLayout } from './admin-layout/admin-layout';

export const ADMIN_ROUTES: Routes = [
  {
    path: '',
    component: AdminLayout,
    children: [
      {
        path: '',
        loadComponent: () => import('./dashboard/dashboard').then(m => m.Dashboard),
        title: 'Dashboard | ShopSphere Admin'
      }
    ]
  }
];
