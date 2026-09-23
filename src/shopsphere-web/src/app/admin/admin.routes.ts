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
        path: 'inventory',
        loadComponent: () => import('./inventory/inventory-list/inventory-list').then(m => m.InventoryList),
        title: 'Inventory | ShopSphere Admin'
      },
      {
        path: 'orders',
        loadComponent: () => import('./orders/admin-order-list/admin-order-list').then(m => m.AdminOrderList),
        title: 'Orders | ShopSphere Admin'
      },
      {
        path: 'orders/:id',
        loadComponent: () => import('./orders/admin-order-detail/admin-order-detail').then(m => m.AdminOrderDetail),
        title: 'Order | ShopSphere Admin'
      },
      {
        path: 'customers',
        loadComponent: () => import('./customers/customer-list/customer-list').then(m => m.CustomerList),
        title: 'Customers | ShopSphere Admin'
      },
      {
        path: 'coupons',
        loadComponent: () => import('./coupons/coupon-list/coupon-list').then(m => m.CouponList),
        title: 'Coupons | ShopSphere Admin'
      },
      {
        path: 'reports',
        loadComponent: () => import('./reports/reports-page/reports-page').then(m => m.ReportsPage),
        title: 'Reports | ShopSphere Admin'
      },
      {
        path: 'categories',
        loadComponent: () => import('./categories/admin-categories/admin-categories').then(m => m.AdminCategories),
        title: 'Categories | ShopSphere Admin'
      }
    ]
  }
];
