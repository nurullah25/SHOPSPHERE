import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { CurrencyPipe, DecimalPipe } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCardModule } from '@angular/material/card';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTableModule } from '@angular/material/table';
import { provideNativeDateAdapter } from '@angular/material/core';
import { BehaviorSubject, combineLatest, catchError, debounceTime, finalize, of, switchMap } from 'rxjs';
import { AdminInsightsService } from '../../services/admin-insights.service';
import {
  OrderStatusSummary,
  ReportRange,
  SalesByCategory,
  SalesByPeriod,
  SalesByProduct
} from '../../../core/models/admin-insights.models';
import { BarChart, BarChartPoint } from '../../../shared/components/bar-chart/bar-chart';
import { OrderStatusChip } from '../../../shared/components/order-status-chip/order-status-chip';

@Component({
  selector: 'app-reports-page',
  providers: [provideNativeDateAdapter()],
  imports: [
    CurrencyPipe,
    DecimalPipe,
    ReactiveFormsModule,
    MatButtonToggleModule,
    MatCardModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatInputModule,
    MatTableModule,
    MatTabsModule,
    BarChart,
    OrderStatusChip
  ],
  templateUrl: './reports-page.html',
  styleUrl: './reports-page.scss'
})
export class ReportsPage {
  private readonly insights = inject(AdminInsightsService);
  private readonly fb = inject(FormBuilder);

  private readonly range$ = new BehaviorSubject<ReportRange>({ groupBy: 'day', top: 10 });

  protected readonly loading = signal(false);
  protected readonly salesByDate = signal<SalesByPeriod[]>([]);
  protected readonly salesByProduct = signal<SalesByProduct[]>([]);
  protected readonly salesByCategory = signal<SalesByCategory[]>([]);
  protected readonly orderStatus = signal<OrderStatusSummary[]>([]);

  protected readonly productColumns = ['product', 'units', 'revenue'];
  protected readonly categoryColumns = ['category', 'units', 'revenue'];
  protected readonly statusColumns = ['status', 'orders', 'total'];

  protected readonly filterForm = this.fb.group({
    from: [this.daysAgo(29)],
    to: [new Date()],
    groupBy: ['day' as 'day' | 'month']
  });

  protected readonly revenuePoints = computed<BarChartPoint[]>(() =>
    this.salesByDate().map(row => ({ label: row.period, value: row.revenue }))
  );

  protected readonly categoryPoints = computed<BarChartPoint[]>(() =>
    this.salesByCategory().map(row => ({ label: row.categoryName, value: row.revenue }))
  );

  protected readonly totals = computed(() => {
    const rows = this.salesByDate();
    return {
      revenue: rows.reduce((sum, row) => sum + row.revenue, 0),
      orders: rows.reduce((sum, row) => sum + row.orders, 0),
      units: rows.reduce((sum, row) => sum + row.units, 0)
    };
  });

  constructor() {
    this.filterForm.valueChanges.pipe(debounceTime(300), takeUntilDestroyed()).subscribe(value => {
      this.range$.next({
        from: value.from ? this.toDateOnly(value.from) : null,
        to: value.to ? this.toDateOnly(value.to) : null,
        groupBy: value.groupBy ?? 'day',
        top: 10
      });
    });

    // All four reports share the same date range, so they reload together
    this.range$
      .pipe(
        switchMap(range => {
          this.loading.set(true);
          return combineLatest([
            this.insights.getSalesByDate(range).pipe(catchError(() => of([]))),
            this.insights.getSalesByProduct(range).pipe(catchError(() => of([]))),
            this.insights.getSalesByCategory(range).pipe(catchError(() => of([]))),
            this.insights.getOrderStatusSummary(range).pipe(catchError(() => of([])))
          ]).pipe(finalize(() => this.loading.set(false)));
        }),
        takeUntilDestroyed()
      )
      .subscribe(([byDate, byProduct, byCategory, byStatus]) => {
        this.salesByDate.set(byDate);
        this.salesByProduct.set(byProduct);
        this.salesByCategory.set(byCategory);
        this.orderStatus.set(byStatus);
      });
  }

  private daysAgo(days: number): Date {
    const date = new Date();
    date.setDate(date.getDate() - days);
    return date;
  }

  // Send plain dates so the server doesn't shift them by the browser time zone
  private toDateOnly(date: Date): string {
    return `${date.getFullYear()}-${`${date.getMonth() + 1}`.padStart(2, '0')}-${`${date.getDate()}`.padStart(2, '0')}`;
  }
}
