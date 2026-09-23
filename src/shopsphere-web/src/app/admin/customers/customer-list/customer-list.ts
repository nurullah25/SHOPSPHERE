import { Component, inject, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { PageEvent, MatPaginatorModule } from '@angular/material/paginator';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { BehaviorSubject, catchError, debounceTime, distinctUntilChanged, filter, finalize, of, switchMap } from 'rxjs';
import { AdminInsightsService } from '../../services/admin-insights.service';
import { CustomerListItem } from '../../../core/models/admin-insights.models';
import { PagedResult } from '../../../core/models/catalog.models';
import { ConfirmDialogService } from '../../../shared/components/confirm-dialog/confirm-dialog';
import { CustomerDetailDialog } from '../customer-detail-dialog/customer-detail-dialog';
import { EmptyState } from '../../../shared/components/empty-state/empty-state';
import { NotificationService } from '../../../core/services/notification.service';

interface CustomerQuery {
  search: string | null;
  isActive: boolean | null;
  page: number;
  pageSize: number;
}

@Component({
  selector: 'app-customer-list',
  imports: [
    CurrencyPipe,
    DatePipe,
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatSelectModule,
    MatTableModule,
    EmptyState
  ],
  templateUrl: './customer-list.html',
  styleUrl: './customer-list.scss'
})
export class CustomerList {
  private readonly insights = inject(AdminInsightsService);
  private readonly dialog = inject(MatDialog);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly notifications = inject(NotificationService);
  private readonly fb = inject(FormBuilder);

  private readonly query$ = new BehaviorSubject<CustomerQuery>({ search: null, isActive: null, page: 1, pageSize: 20 });

  protected readonly columns = ['customer', 'orders', 'spent', 'lastOrder', 'joined', 'status', 'actions'];
  protected readonly result = signal<PagedResult<CustomerListItem> | null>(null);
  protected readonly loading = signal(true);

  protected readonly filterForm = this.fb.group({
    search: [''],
    isActive: [null as boolean | null]
  });

  constructor() {
    this.filterForm.valueChanges
      .pipe(debounceTime(300), distinctUntilChanged((a, b) => JSON.stringify(a) === JSON.stringify(b)), takeUntilDestroyed())
      .subscribe(value => {
        this.query$.next({
          ...this.query$.value,
          search: value.search?.trim() || null,
          isActive: value.isActive ?? null,
          page: 1
        });
      });

    this.query$
      .pipe(
        switchMap(query => {
          this.loading.set(true);
          return this.insights.getCustomers(query.search, query.isActive, query.page, query.pageSize).pipe(
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

  openCustomer(customer: CustomerListItem): void {
    this.dialog.open(CustomerDetailDialog, { data: customer.id, width: '560px' });
  }

  toggleStatus(customer: CustomerListItem): void {
    const activating = !customer.isActive;

    const confirmed$ = activating
      ? this.confirmDialog.confirm({
          title: `Reactivate ${customer.name}?`,
          message: 'The customer will be able to sign in and order again.',
          confirmText: 'Reactivate'
        })
      : this.confirmDialog.confirm({
          title: `Deactivate ${customer.name}?`,
          message: 'They will be signed out and cannot sign in again until reactivated. Their orders are kept.',
          confirmText: 'Deactivate',
          destructive: true
        });

    confirmed$
      .pipe(
        filter(Boolean),
        switchMap(() => this.insights.setCustomerStatus(customer.id, activating))
      )
      .subscribe(() => {
        this.notifications.success(`${customer.name} was ${activating ? 'reactivated' : 'deactivated'}.`);
        this.query$.next({ ...this.query$.value });
      });
  }
}
