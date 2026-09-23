import { Component, effect, inject, input, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { filter, finalize, of, switchMap } from 'rxjs';
import { AdminOrderService } from '../../services/admin-order.service';
import { AdminOrder } from '../../../core/models/checkout.models';
import { ConfirmDialogService } from '../../../shared/components/confirm-dialog/confirm-dialog';
import { EmptyState } from '../../../shared/components/empty-state/empty-state';
import { NotificationService } from '../../../core/services/notification.service';
import { OrderStatusChip } from '../../../shared/components/order-status-chip/order-status-chip';

@Component({
  selector: 'app-admin-order-detail',
  imports: [
    CurrencyPipe,
    DatePipe,
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    EmptyState,
    OrderStatusChip
  ],
  templateUrl: './admin-order-detail.html',
  styleUrl: './admin-order-detail.scss'
})
export class AdminOrderDetail {
  private readonly orders = inject(AdminOrderService);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly notifications = inject(NotificationService);

  readonly id = input.required<string>();

  protected readonly order = signal<AdminOrder | null>(null);
  protected readonly loading = signal(true);
  protected readonly busy = signal(false);
  protected readonly note = new FormControl('', { nonNullable: true });

  constructor() {
    effect(() => this.load(Number(this.id())));
  }

  changeStatus(status: string): void {
    const order = this.order();
    if (!order) return;

    // Cancelling puts stock back and refunds, so it gets a confirmation
    const confirmed$ =
      status === 'Cancelled'
        ? this.confirmDialog.confirm({
            title: `Cancel order ${order.orderNumber}?`,
            message: 'The items go back into stock, the coupon use is released and the payment is refunded.',
            confirmText: 'Cancel order',
            destructive: true
          })
        : of(true);

    confirmed$
      .pipe(
        filter(Boolean),
        switchMap(() => {
          this.busy.set(true);
          return this.orders
            .changeStatus(order.id, status, this.note.value.trim() || null)
            .pipe(finalize(() => this.busy.set(false)));
        })
      )
      .subscribe(updated => {
        this.order.set(updated);
        this.note.reset();
        this.notifications.success(`Order ${updated.orderNumber} is now ${updated.status.toLowerCase()}.`);
      });
  }

  private load(id: number): void {
    this.loading.set(true);
    this.orders
      .getOrder(id)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: order => this.order.set(order),
        error: () => this.order.set(null)
      });
  }
}
