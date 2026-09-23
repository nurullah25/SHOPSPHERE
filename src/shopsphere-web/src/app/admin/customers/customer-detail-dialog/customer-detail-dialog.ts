import { Component, inject, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { finalize } from 'rxjs';
import { AdminInsightsService } from '../../services/admin-insights.service';
import { CustomerDetail } from '../../../core/models/admin-insights.models';
import { OrderStatusChip } from '../../../shared/components/order-status-chip/order-status-chip';

@Component({
  selector: 'app-customer-detail-dialog',
  imports: [CurrencyPipe, DatePipe, RouterLink, MatDialogModule, MatButtonModule, OrderStatusChip],
  template: `
    @if (customer(); as details) {
      <h2 mat-dialog-title>{{ details.name }}</h2>

      <mat-dialog-content>
        <p class="muted">
          {{ details.email }}
          @if (details.phoneNumber) {
            &middot; {{ details.phoneNumber }}
          }
        </p>

        <div class="stats">
          <div>
            <span class="label">Orders</span>
            <span class="value">{{ details.orderCount }}</span>
          </div>
          <div>
            <span class="label">Spent</span>
            <span class="value">{{ details.totalSpent | currency: 'USD' }}</span>
          </div>
          <div>
            <span class="label">Joined</span>
            <span class="value">{{ details.createdAt | date: 'mediumDate' }}</span>
          </div>
          <div>
            <span class="label">Last seen</span>
            <span class="value">{{ details.lastLoginAt ? (details.lastLoginAt | date: 'mediumDate') : '—' }}</span>
          </div>
        </div>

        <h3>Recent orders</h3>
        @for (order of details.recentOrders; track order.id) {
          <a class="order-row" [routerLink]="['/admin/orders', order.id]" mat-dialog-close>
            <span>
              {{ order.orderNumber }}
              <span class="muted">{{ order.placedAt | date: 'mediumDate' }}</span>
            </span>
            <app-order-status-chip [status]="order.status" />
            <span>{{ order.total | currency: 'USD' }}</span>
          </a>
        } @empty {
          <p class="muted">This customer hasn't ordered yet.</p>
        }
      </mat-dialog-content>

      <mat-dialog-actions align="end">
        <button mat-button mat-dialog-close>Close</button>
      </mat-dialog-actions>
    }
  `,
  styles: `
    mat-dialog-content {
      min-width: 360px;
    }

    .stats {
      display: grid;
      grid-template-columns: repeat(2, 1fr);
      gap: 12px;
      margin: 16px 0;
    }

    .stats div {
      display: flex;
      flex-direction: column;
    }

    .label {
      font: var(--mat-sys-label-medium);
      color: var(--mat-sys-on-surface-variant);
    }

    .value {
      font: var(--mat-sys-title-medium);
    }

    h3 {
      margin: 16px 0 8px;
      font: var(--mat-sys-title-small);
    }

    .order-row {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 12px;
      padding: 8px 0;
      border-bottom: 1px solid var(--mat-sys-outline-variant);
      text-decoration: none;
      color: inherit;

      span {
        display: flex;
        flex-direction: column;
      }
    }

    .muted {
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }
  `
})
export class CustomerDetailDialog {
  private readonly insights = inject(AdminInsightsService);
  private readonly dialogRef = inject(MatDialogRef<CustomerDetailDialog>);

  protected readonly customer = signal<CustomerDetail | null>(null);
  protected readonly loading = signal(true);

  constructor() {
    const customerId = inject<number>(MAT_DIALOG_DATA);

    this.insights
      .getCustomer(customerId)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: customer => this.customer.set(customer),
        error: () => this.dialogRef.close()
      });
  }
}
