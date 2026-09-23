import { Component, effect, inject, input, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatRadioModule } from '@angular/material/radio';
import { finalize } from 'rxjs';
import { CheckoutService } from '../checkout.service';
import { Order, PAYMENT_METHODS } from '../../core/models/checkout.models';
import { EmptyState } from '../../shared/components/empty-state/empty-state';
import { NotificationService } from '../../core/services/notification.service';
import { ProductPhoto } from '../../shared/components/product-photo/product-photo';

@Component({
  selector: 'app-order-confirmation',
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
    ProductPhoto
  ],
  templateUrl: './order-confirmation.html',
  styleUrl: './order-confirmation.scss'
})
export class OrderConfirmation {
  private readonly checkout = inject(CheckoutService);
  private readonly notifications = inject(NotificationService);

  // Bound from the :orderNumber route parameter
  readonly orderNumber = input.required<string>();

  protected readonly paymentMethods = PAYMENT_METHODS;
  protected readonly order = signal<Order | null>(null);
  protected readonly loading = signal(true);
  protected readonly retrying = signal(false);
  protected readonly retryToken = new FormControl(PAYMENT_METHODS[0].token, { nonNullable: true });

  constructor() {
    effect(() => {
      const orderNumber = this.orderNumber();
      this.loading.set(true);
      this.checkout
        .getOrder(orderNumber)
        .pipe(finalize(() => this.loading.set(false)))
        .subscribe({
          next: order => this.order.set(order),
          error: () => this.order.set(null)
        });
    });
  }

  retryPayment(): void {
    const order = this.order();
    if (!order) return;

    this.retrying.set(true);
    this.checkout
      .retryPayment(order.orderNumber, this.retryToken.value)
      .pipe(finalize(() => this.retrying.set(false)))
      .subscribe(result => {
        if (result.paymentSucceeded) {
          this.notifications.success('Payment received. Your order is confirmed.');
        }
        this.checkout.getOrder(order.orderNumber).subscribe(updated => this.order.set(updated));
      });
  }
}
