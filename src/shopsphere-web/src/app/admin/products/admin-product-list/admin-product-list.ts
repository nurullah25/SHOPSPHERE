import { Component, inject, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { PageEvent, MatPaginatorModule } from '@angular/material/paginator';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { BehaviorSubject, catchError, debounceTime, distinctUntilChanged, filter, finalize, of, switchMap } from 'rxjs';
import { AdminCatalogService } from '../../services/admin-catalog.service';
import {
  AdminProductListItem,
  AdminProductQuery,
  AdminProductSort
} from '../../../core/models/admin-catalog.models';
import { ConfirmDialogService } from '../../../shared/components/confirm-dialog/confirm-dialog';
import { EmptyState } from '../../../shared/components/empty-state/empty-state';
import { NotificationService } from '../../../core/services/notification.service';
import { PagedResult } from '../../../core/models/catalog.models';
import { flattenTree } from '../../../shared/utils/category-tree';

@Component({
  selector: 'app-admin-product-list',
  imports: [
    CurrencyPipe,
    DatePipe,
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatSelectModule,
    MatTableModule,
    MatTooltipModule,
    EmptyState
  ],
  templateUrl: './admin-product-list.html',
  styleUrl: './admin-product-list.scss'
})
export class AdminProductList {
  private readonly catalog = inject(AdminCatalogService);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly notifications = inject(NotificationService);
  private readonly fb = inject(FormBuilder);

  private readonly query$ = new BehaviorSubject<AdminProductQuery>({ sort: 'newest', page: 1, pageSize: 20 });

  protected readonly columns = ['image', 'name', 'category', 'price', 'stock', 'status', 'actions'];
  protected readonly loading = signal(true);
  protected readonly result = signal<PagedResult<AdminProductListItem> | null>(null);
  protected query: AdminProductQuery = this.query$.value;

  protected readonly categories = toSignal(
    this.catalog.getCategories().pipe(
      catchError(() => of([]))
    ),
    { initialValue: [] }
  );

  protected readonly filterForm = this.fb.group({
    search: [''],
    categoryId: [null as number | null],
    isActive: [null as boolean | null],
    sort: ['newest' as AdminProductSort]
  });

  constructor() {
    this.filterForm.valueChanges
      .pipe(debounceTime(300), distinctUntilChanged((a, b) => JSON.stringify(a) === JSON.stringify(b)), takeUntilDestroyed())
      .subscribe(value => {
        this.query$.next({
          ...this.query$.value,
          search: value.search?.trim() || undefined,
          categoryId: value.categoryId ?? undefined,
          isActive: value.isActive ?? undefined,
          sort: value.sort ?? 'newest',
          page: 1
        });
      });

    this.query$
      .pipe(
        switchMap(query => {
          this.query = query;
          this.loading.set(true);
          return this.catalog.getProducts(query).pipe(
            catchError(() => of(null)),
            finalize(() => this.loading.set(false))
          );
        }),
        takeUntilDestroyed()
      )
      .subscribe(result => this.result.set(result));
  }

  protected readonly categoryOptions = () =>
    flattenTree(this.categories()).map(item => ({
      id: item.node.id,
      label: `${'  '.repeat(item.depth)}${item.node.name}`
    }));

  changePage(event: PageEvent): void {
    this.query$.next({ ...this.query$.value, page: event.pageIndex + 1, pageSize: event.pageSize });
  }

  reload(): void {
    this.query$.next({ ...this.query$.value });
  }

  deleteProduct(product: AdminProductListItem): void {
    this.confirmDialog
      .confirm({
        title: `Delete ${product.name}?`,
        message: 'This permanently removes the product and its images. Products that appear in past orders cannot be deleted.',
        confirmText: 'Delete',
        destructive: true
      })
      .pipe(
        filter(Boolean),
        switchMap(() => this.catalog.deleteProduct(product.id))
      )
      .subscribe(() => {
        this.notifications.success(`${product.name} was deleted.`);
        this.reload();
      });
  }
}
