import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('./home/home').then(m => m.Home),
    title: 'ShopSphere'
  },
  {
    path: '**',
    loadComponent: () => import('./core/not-found/not-found').then(m => m.NotFound),
    title: 'Page not found | ShopSphere'
  }
];
