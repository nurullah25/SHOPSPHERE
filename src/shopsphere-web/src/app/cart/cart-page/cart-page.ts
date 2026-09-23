import { Component, inject, signal } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { filter, switchMap } from 'rxjs';
import { CartService } from '../cart.service';
import { CartItem } from '../../core/models/cart.models';
import { ConfirmDialogService } from '../../shared/components/confirm-dialog/confirm-dialog';
import { EmptyState } from '../../shared/components/empty-state/empty-state';
import { NotificationService } from '../../core/services/notification.service';
import { ProductPhoto } from '../../shared/components/product-photo/product-photo';
import { QuantitySelector } from '../../shared/components/quantity-selector/quantity-selector';

const FREE_SHIPPING_THRESHOLD = 75;
const SHIPPING_COST = 5.99;

@Component({
  selector: 'app-cart-page',
  imports: [
    CurrencyPipe,
    RouterLink,
    MatButtonModule,
    MatCardModule,
    MatIconModule,
    EmptyState,
    ProductPhoto,
    QuantitySelector
  ],
  templateUrl: './cart-page.html',
  styleUrl: './cart-page.scss'
})
export class CartPage {
  private readonly cartService = inject(CartService);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly notifications = inject(NotificationService);

  protected readonly cart = toSignal(this.cartService.cart$, { requireSync: true });
  protected readonly updating = signal(false);

  // Mirrors the shipping rule the API applies at checkout
  protected shippingCost(subtotal: number): number {
    return subtotal === 0 || subtotal >= FREE_SHIPPING_THRESHOLD ? 0 : SHIPPING_COST;
  }

  protected amountToFreeShipping(subtotal: number): number {
    return Math.max(0, FREE_SHIPPING_THRESHOLD - subtotal);
  }

  changeQuantity(item: CartItem, quantity: number): void {
    this.updating.set(true);
    this.cartService.updateQuantity(item.productId, quantity).subscribe({
      next: () => this.updating.set(false),
      error: () => {
        this.updating.set(false);
        this.cartService.refresh();
      }
    });
  }

  remove(item: CartItem): void {
    this.cartService.removeItem(item.productId).subscribe(() =>
      this.notifications.success(`${item.name} was removed from your cart.`)
    );
  }

  clear(): void {
    this.confirmDialog
      .confirm({
        title: 'Empty your cart?',
        message: 'All products will be removed from the cart.',
        confirmText: 'Empty cart',
        destructive: true
      })
      .pipe(
        filter(Boolean),
        switchMap(() => this.cartService.clear())
      )
      .subscribe();
  }
}
