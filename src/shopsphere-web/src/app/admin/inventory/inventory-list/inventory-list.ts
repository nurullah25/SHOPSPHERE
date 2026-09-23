import { Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { PageEvent, MatPaginatorModule } from '@angular/material/paginator';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { BehaviorSubject, catchError, debounceTime, distinctUntilChanged, filter, finalize, of, switchMap } from 'rxjs';
import { AdminInventoryService } from '../../services/admin-inventory.service';
import { InventoryItem, InventoryQuery } from '../../../core/models/inventory.models';
import { PagedResult } from '../../../core/models/catalog.models';
import { EmptyState } from '../../../shared/components/empty-state/empty-state';
import { MovementHistoryDialog } from '../movement-history-dialog/movement-history-dialog';
import { NotificationService } from '../../../core/services/notification.service';
import { StockAdjustmentDialog } from '../stock-adjustment-dialog/stock-adjustment-dialog';

@Component({
  selector: 'app-inventory-list',
  imports: [
    DatePipe,
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatSelectModule,
    MatTableModule,
    MatTooltipModule,
    EmptyState
  ],
  templateUrl: './inventory-list.html',
  styleUrl: './inventory-list.scss'
})
export class InventoryList {
  private readonly inventory = inject(AdminInventoryService);
  private readonly dialog = inject(MatDialog);
  private readonly notifications = inject(NotificationService);
  private readonly fb = inject(FormBuilder);

  private readonly query$ = new BehaviorSubject<InventoryQuery>({ sort: 'stock_asc', page: 1, pageSize: 25 });

  protected readonly columns = ['product', 'category', 'stock', 'reserved', 'threshold', 'updated', 'actions'];
  protected readonly result = signal<PagedResult<InventoryItem> | null>(null);
  protected readonly loading = signal(true);

  protected readonly filterForm = this.fb.group({
    search: [''],
    lowStock: [false],
    sort: ['stock_asc' as InventoryQuery['sort']]
  });

  constructor() {
    this.filterForm.valueChanges
      .pipe(debounceTime(300), distinctUntilChanged((a, b) => JSON.stringify(a) === JSON.stringify(b)), takeUntilDestroyed())
      .subscribe(value => {
        this.query$.next({
          ...this.query$.value,
          search: value.search?.trim() || null,
          lowStock: value.lowStock || null,
          sort: value.sort ?? 'stock_asc',
          page: 1
        });
      });

    this.query$
      .pipe(
        switchMap(query => {
          this.loading.set(true);
          return this.inventory.getInventory(query).pipe(
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

  adjust(item: InventoryItem): void {
    this.dialog
      .open(StockAdjustmentDialog, { data: item, width: '420px' })
      .afterClosed()
      .pipe(filter(Boolean))
      .subscribe(movement => {
        this.notifications.success(`${item.name} now has ${movement.quantityAfter} in stock.`);
        this.reload();
      });
  }

  showHistory(item: InventoryItem): void {
    this.dialog.open(MovementHistoryDialog, { data: item, width: '560px' });
  }

  private reload(): void {
    this.query$.next({ ...this.query$.value });
  }
}
