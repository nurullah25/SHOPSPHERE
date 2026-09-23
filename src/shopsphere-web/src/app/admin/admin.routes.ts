import { Routes } from '@angular/router';
import { AdminLayout } from './admin-layout/admin-layout';
import { unsavedChangesGuard } from '../core/guards/unsaved-changes.guard';

export const ADMIN_ROUTES: Routes = [
  {
    path: '',
    component: AdminLayout,
    children: [
      {
        path: '',
        loadComponent: () => import('./dashboard/dashboard').then(m => m.Dashboard),
        title: 'Dashboard | ShopSphere Admin'
      },
      {
        path: 'products',
        loadComponent: () => import('./products/admin-product-list/admin-product-list').then(m => m.AdminProductList),
        title: 'Products | ShopSphere Admin'
      },
      {
        path: 'products/new',
        loadComponent: () => import('./products/admin-product-form/admin-product-form').then(m => m.AdminProductForm),
        canDeactivate: [unsavedChangesGuard],
        title: 'New product | ShopSphere Admin'
      },
      {
        path: 'products/:id',
        loadComponent: () => import('./products/admin-product-form/admin-product-form').then(m => m.AdminProductForm),
        canDeactivate: [unsavedChangesGuard],
        title: 'Edit product | ShopSphere Admin'
      },
      {
        path: 'categories',
        loadComponent: () => import('./categories/admin-categories/admin-categories').then(m => m.AdminCategories),
        title: 'Categories | ShopSphere Admin'
      }
    ]
  }
];
