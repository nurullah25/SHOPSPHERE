import { Component, inject, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { PageEvent, MatPaginatorModule } from '@angular/material/paginator';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { provideNativeDateAdapter } from '@angular/material/core';
import { BehaviorSubject, catchError, debounceTime, distinctUntilChanged, finalize, of, switchMap } from 'rxjs';
import { AdminOrderQuery, AdminOrderService } from '../../services/admin-order.service';
import { AdminOrderListItem } from '../../../core/models/checkout.models';
import { PagedResult } from '../../../core/models/catalog.models';
import { EmptyState } from '../../../shared/components/empty-state/empty-state';
import { OrderStatusChip } from '../../../shared/components/order-status-chip/order-status-chip';

const ORDER_STATUSES = ['Pending', 'Confirmed', 'Processing', 'Shipped', 'Delivered', 'Cancelled'];

@Component({
  selector: 'app-admin-order-list',
  providers: [provideNativeDateAdapter()],
  imports: [
    CurrencyPipe,
    DatePipe,
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatSelectModule,
    MatTableModule,
    EmptyState,
    OrderStatusChip
  ],
  templateUrl: './admin-order-list.html',
  styleUrl: './admin-order-list.scss'
})
export class AdminOrderList {
  private readonly orders = inject(AdminOrderService);
  private readonly fb = inject(FormBuilder);

  private readonly query$ = new BehaviorSubject<AdminOrderQuery>({ page: 1, pageSize: 20 });

  protected readonly statuses = ORDER_STATUSES;
  protected readonly columns = ['orderNumber', 'customer', 'placedAt', 'items', 'total', 'status', 'actions'];
  protected readonly result = signal<PagedResult<AdminOrderListItem> | null>(null);
  protected readonly loading = signal(true);

  protected readonly filterForm = this.fb.group({
    search: [''],
    status: [null as string | null],
    from: [null as Date | null],
    to: [null as Date | null]
  });

  constructor() {
    this.filterForm.valueChanges
      .pipe(debounceTime(300), distinctUntilChanged((a, b) => JSON.stringify(a) === JSON.stringify(b)), takeUntilDestroyed())
      .subscribe(value => {
        this.query$.next({
          ...this.query$.value,
          search: value.search?.trim() || null,
          status: value.status ?? null,
          from: value.from ? value.from.toISOString() : null,
          to: value.to ? value.to.toISOString() : null,
          page: 1
        });
      });

    this.query$
      .pipe(
        switchMap(query => {
          this.loading.set(true);
          return this.orders.getOrders(query).pipe(
            catchError(() => of(null)),
            finalize(() => this.loading.set(false))
          );
        }),
        takeUntilDestroyed()
      )
      .subscribe(result => this.result.set(result));
  }

  changePage(event: PageEvent): void {
    this.query$.next({ ...this.query$.value, page: event.pageIndex + 1, pageSize: event.pageSize });
  }
}
