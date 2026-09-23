import { Component, inject, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { PageEvent, MatPaginatorModule } from '@angular/material/paginator';
import { finalize } from 'rxjs';
import { OrderService } from '../order.service';
import { OrderSummary } from '../../core/models/checkout.models';
import { PagedResult } from '../../core/models/catalog.models';
import { EmptyState } from '../../shared/components/empty-state/empty-state';
import { OrderStatusChip } from '../../shared/components/order-status-chip/order-status-chip';
import { ProductPhoto } from '../../shared/components/product-photo/product-photo';

@Component({
  selector: 'app-order-list',
  imports: [
    CurrencyPipe,
    DatePipe,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatPaginatorModule,
    EmptyState,
    OrderStatusChip,
    ProductPhoto
  ],
  templateUrl: './order-list.html',
  styleUrl: './order-list.scss'
})
export class OrderList {
  private readonly orders = inject(OrderService);

  protected readonly result = signal<PagedResult<OrderSummary> | null>(null);
  protected readonly loading = signal(true);

  constructor() {
    this.load(1);
  }

  changePage(event: PageEvent): void {
    this.load(event.pageIndex + 1, event.pageSize);
  }

  private load(page: number, pageSize = 10): void {
    this.loading.set(true);
    this.orders
      .getOrders(page, pageSize)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: result => this.result.set(result),
        error: () => this.result.set(null)
      });
  }
}
