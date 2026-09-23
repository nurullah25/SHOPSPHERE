import { Component, effect, inject, input, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatRadioModule } from '@angular/material/radio';
import { filter, finalize, switchMap } from 'rxjs';
import { OrderService } from '../order.service';
import { CartService } from '../../cart/cart.service';
import { Order, PAYMENT_METHODS } from '../../core/models/checkout.models';
import { ConfirmDialogService } from '../../shared/components/confirm-dialog/confirm-dialog';
import { EmptyState } from '../../shared/components/empty-state/empty-state';
import { NotificationService } from '../../core/services/notification.service';
import { OrderStatusChip } from '../../shared/components/order-status-chip/order-status-chip';
import { ProductPhoto } from '../../shared/components/product-photo/product-photo';

@Component({
  selector: 'app-order-detail',
  imports: [
    CurrencyPipe,
    DatePipe,
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatCardModule,
    MatIconModule,
    MatRadioModule,
    EmptyState,
    OrderStatusChip,
    ProductPhoto
  ],
  templateUrl: './order-detail.html',
  styleUrl: './order-detail.scss'
})
export class OrderDetail {
  private readonly orders = inject(OrderService);
  private readonly cart = inject(CartService);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly notifications = inject(NotificationService);

  // orderNumber comes from the route, justPlaced from the route data of
  // the checkout success route
  readonly orderNumber = input.required<string>();
  readonly justPlaced = input(false);

  protected readonly paymentMethods = PAYMENT_METHODS;
  protected readonly order = signal<Order | null>(null);
  protected readonly loading = signal(true);
  protected readonly busy = signal(false);
  protected readonly retryToken = new FormControl(PAYMENT_METHODS[0].token, { nonNullable: true });

  constructor() {
    effect(() => this.load(this.orderNumber()));
  }

  retryPayment(): void {
    const order = this.order();
    if (!order) return;

    this.busy.set(true);
    this.orders
      .retryPayment(order.orderNumber, this.retryToken.value)
      .pipe(finalize(() => this.busy.set(false)))
      .subscribe(result => {
        if (result.paymentSucceeded) {
          this.notifications.success('Payment received. Your order is confirmed.');
        }
        this.load(order.orderNumber);
      });
  }

  cancelOrder(): void {
    const order = this.order();
    if (!order) return;

    this.confirmDialog
      .confirm({
        title: `Cancel order ${order.orderNumber}?`,
        message: 'The items go back into stock and any payment is refunded. This cannot be undone.',
        confirmText: 'Cancel order',
        destructive: true
      })
      .pipe(
        filter(Boolean),
        switchMap(() => {
          this.busy.set(true);
          return this.orders.cancel(order.orderNumber, null).pipe(finalize(() => this.busy.set(false)));
        })
      )
      .subscribe(updated => {
        this.order.set(updated);
        this.notifications.success('Your order has been cancelled.');
        // Stock changed, so a cart holding the same product may look different
        this.cart.refresh();
      });
  }

  private load(orderNumber: string): void {
    this.loading.set(true);
    this.orders
      .getOrder(orderNumber)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: order => this.order.set(order),
        error: () => this.order.set(null)
      });
  }
}
