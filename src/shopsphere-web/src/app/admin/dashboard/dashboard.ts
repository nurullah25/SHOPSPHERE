import { Component, computed, inject, signal } from '@angular/core';
import { CurrencyPipe, DatePipe, DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { finalize } from 'rxjs';
import { AdminInsightsService } from '../services/admin-insights.service';
import { AuthService } from '../../core/auth/auth.service';
import { Dashboard as DashboardData } from '../../core/models/admin-insights.models';
import { BarChart, BarChartPoint } from '../../shared/components/bar-chart/bar-chart';
import { OrderStatusChip } from '../../shared/components/order-status-chip/order-status-chip';

@Component({
  selector: 'app-admin-dashboard',
  imports: [
    CurrencyPipe,
    DatePipe,
    DecimalPipe,
    RouterLink,
    MatButtonModule,
    MatButtonToggleModule,
    MatCardModule,
    MatIconModule,
    BarChart,
    OrderStatusChip
  ],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss'
})
export class Dashboard {
  private readonly insights = inject(AdminInsightsService);

  protected readonly auth = inject(AuthService);
  protected readonly data = signal<DashboardData | null>(null);
  protected readonly loading = signal(true);
  protected readonly days = signal(30);

  protected readonly revenuePoints = computed<BarChartPoint[]>(() =>
    (this.data()?.revenueByDay ?? []).map(day => ({
      label: new Date(day.date).toLocaleDateString('en-US', { day: 'numeric', month: 'short' }),
      value: day.revenue
    }))
  );

  // Percentage change against the previous period of the same length
  protected readonly revenueChange = computed(() => {
    const data = this.data();
    if (!data || data.revenuePreviousPeriod === 0) return null;
    return Math.round(((data.revenueInPeriod - data.revenuePreviousPeriod) / data.revenuePreviousPeriod) * 100);
  });

  constructor() {
    this.load();
  }

  changePeriod(days: number): void {
    this.days.set(days);
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.insights
      .getDashboard(this.days())
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: data => this.data.set(data),
        error: () => this.data.set(null)
      });
  }
}
