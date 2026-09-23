import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { WishlistService } from '../wishlist.service';
import { WishlistItem } from '../../core/models/cart.models';
import { EmptyState } from '../../shared/components/empty-state/empty-state';
import { NotificationService } from '../../core/services/notification.service';
import { Price } from '../../shared/components/price/price';
import { ProductPhoto } from '../../shared/components/product-photo/product-photo';

@Component({
  selector: 'app-wishlist-page',
  imports: [RouterLink, MatButtonModule, MatIconModule, EmptyState, Price, ProductPhoto],
  templateUrl: './wishlist-page.html',
  styleUrl: './wishlist-page.scss'
})
export class WishlistPage {
  private readonly wishlist = inject(WishlistService);
  private readonly notifications = inject(NotificationService);

  protected readonly items = toSignal(this.wishlist.items$, { requireSync: true });
  protected readonly busyProductId = signal<number | null>(null);

  moveToCart(item: WishlistItem): void {
    this.busyProductId.set(item.productId);
    this.wishlist.moveToCart(item.productId).subscribe({
      next: () => {
        this.busyProductId.set(null);
        this.notifications.success(`${item.name} was moved to your cart.`);
      },
      error: () => this.busyProductId.set(null)
    });
  }

  remove(item: WishlistItem): void {
    this.busyProductId.set(item.productId);
    this.wishlist.remove(item.productId).subscribe({
      next: () => this.busyProductId.set(null),
      error: () => this.busyProductId.set(null)
    });
  }
}
