import { Routes } from '@angular/router';
import { adminGuard, authGuard, guestGuard } from './core/guards/auth.guards';

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
        path: 'products',
        loadComponent: () => import('./products/product-list/product-list').then(m => m.ProductList),
        title: 'Products | ShopSphere'
      },
      {
        path: 'products/:slug',
        loadComponent: () => import('./products/product-detail/product-detail').then(m => m.ProductDetail)
      },
      {
        path: 'cart',
        canActivate: [authGuard],
        loadComponent: () => import('./cart/cart-page/cart-page').then(m => m.CartPage),
        title: 'Cart | ShopSphere'
      },
      {
        path: 'wishlist',
        canActivate: [authGuard],
        loadComponent: () => import('./wishlist/wishlist-page/wishlist-page').then(m => m.WishlistPage),
        title: 'Wishlist | ShopSphere'
      },
      {
        path: 'checkout',
        canActivate: [authGuard],
        loadComponent: () => import('./checkout/checkout-page/checkout-page').then(m => m.CheckoutPage),
        title: 'Checkout | ShopSphere'
      },
      {
        path: 'checkout/success/:orderNumber',
        canActivate: [authGuard],
        // Same page as the order details, with the confirmation banner
        data: { justPlaced: true },
        loadComponent: () => import('./orders/order-detail/order-detail').then(m => m.OrderDetail),
        title: 'Order confirmed | ShopSphere'
      },
      {
        path: 'account/profile',
        canActivate: [authGuard],
        loadComponent: () => import('./account/account-page/account-page').then(m => m.AccountPage),
        title: 'My account | ShopSphere'
      },
      {
        path: 'account/orders',
        canActivate: [authGuard],
        loadComponent: () => import('./orders/order-list/order-list').then(m => m.OrderList),
        title: 'My orders | ShopSphere'
      },
      {
        path: 'account/orders/:orderNumber',
        canActivate: [authGuard],
        loadComponent: () => import('./orders/order-detail/order-detail').then(m => m.OrderDetail),
        title: 'Order | ShopSphere'
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
