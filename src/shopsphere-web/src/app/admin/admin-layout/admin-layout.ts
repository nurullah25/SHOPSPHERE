import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { BreakpointObserver } from '@angular/cdk/layout';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatListModule } from '@angular/material/list';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { map } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';

interface AdminNavItem {
  label: string;
  icon: string;
  path: string;
}

@Component({
  selector: 'app-admin-layout',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, MatSidenavModule, MatToolbarModule, MatListModule, MatIconModule, MatButtonModule],
  templateUrl: './admin-layout.html',
  styleUrl: './admin-layout.scss'
})
export class AdminLayout {
  protected readonly auth = inject(AuthService);

  protected readonly isMobile = toSignal(
    inject(BreakpointObserver).observe('(max-width: 960px)').pipe(map(result => result.matches)),
    { initialValue: false }
  );

  protected readonly navItems: AdminNavItem[] = [
    { label: 'Dashboard', icon: 'dashboard', path: '/admin' },
    { label: 'Products', icon: 'inventory_2', path: '/admin/products' },
    { label: 'Categories', icon: 'category', path: '/admin/categories' },
    { label: 'Inventory', icon: 'inventory', path: '/admin/inventory' },
    { label: 'Orders', icon: 'receipt_long', path: '/admin/orders' },
    { label: 'Customers', icon: 'group', path: '/admin/customers' },
    { label: 'Coupons', icon: 'local_offer', path: '/admin/coupons' },
    { label: 'Reports', icon: 'insights', path: '/admin/reports' },
    { label: 'Activity log', icon: 'history', path: '/admin/activity' }
  ];
}
