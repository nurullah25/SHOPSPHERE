import { Routes } from '@angular/router';
import { adminGuard, guestGuard } from './core/guards/auth.guards';

export const routes: Routes = [
  {
    path: 'admin',
    canActivate: [adminGuard],
    loadChildren: () => import('./admin/admin.routes').then(m => m.ADMIN_ROUTES)
  },
  {
    path: '',
    loadComponent: () => import('./core/layout/store-layout/store-layout').then(m => m.StoreLayout),
    children: [
      {
        path: '',
        loadComponent: () => import('./home/home').then(m => m.Home),
        title: 'ShopSphere'
      },
      {
        path: 'login',
        canActivate: [guestGuard],
        loadComponent: () => import('./auth/login/login').then(m => m.Login),
        title: 'Sign in | ShopSphere'
      },
      {
        path: 'register',
        canActivate: [guestGuard],
        loadComponent: () => import('./auth/register/register').then(m => m.Register),
        title: 'Create account | ShopSphere'
      },
      {
        path: '**',
        loadComponent: () => import('./core/not-found/not-found').then(m => m.NotFound),
        title: 'Page not found | ShopSphere'
      }
    ]
  }
];
