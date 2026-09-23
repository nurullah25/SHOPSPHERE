import { Component, computed, input } from '@angular/core';
import { CurrencyPipe, DecimalPipe } from '@angular/common';

export interface BarChartPoint {
  label: string;
  value: number;
}

// Small inline SVG chart. A charting library would be more than this needs.
@Component({
  selector: 'app-bar-chart',
  imports: [CurrencyPipe, DecimalPipe],
  template: `
    @if (points().length === 0) {
      <p class="empty">No data for this period.</p>
    } @else {
      <div class="chart" [style.--bar-count]="points().length">
        @for (bar of bars(); track bar.label + $index) {
          <div class="column" [title]="bar.label + ': ' + formatValue(bar.value)">
            <div class="bar" [style.height.%]="bar.height"></div>
            <span class="label">{{ bar.showLabel ? bar.label : '' }}</span>
          </div>
        }
      </div>
      <div class="scale">
        <span>0</span>
        <span>
          @if (format() === 'currency') {
            {{ max() | currency: 'USD' : 'symbol' : '1.0-0' }}
          } @else {
            {{ max() | number }}
          }
        </span>
      </div>
    }
  `,
  styles: `
    .chart {
      display: flex;
      align-items: flex-end;
      gap: 4px;
      height: 180px;
      padding-top: 8px;
    }

    .column {
      flex: 1;
      min-width: 0;
      display: flex;
      flex-direction: column;
      justify-content: flex-end;
      align-items: center;
      height: 100%;
    }

    .bar {
      width: 100%;
      /* Keeps a single data point from stretching across the whole chart */
      max-width: 64px;
      min-height: 2px;
      border-radius: 4px 4px 0 0;
      background: var(--mat-sys-primary);
    }

    .label {
      margin-top: 6px;
      font: var(--mat-sys-label-small);
      color: var(--mat-sys-on-surface-variant);
      white-space: nowrap;
      /* Labels are only drawn on some columns, so they can spill into the gap */
      overflow: visible;
    }

    .scale {
      display: flex;
      justify-content: space-between;
      font: var(--mat-sys-label-small);
      color: var(--mat-sys-on-surface-variant);
      border-top: 1px solid var(--mat-sys-outline-variant);
      padding-top: 4px;
    }

    .empty {
      color: var(--mat-sys-on-surface-variant);
      padding: 24px 0;
      text-align: center;
    }
  `
})
export class BarChart {
  readonly points = input.required<BarChartPoint[]>();
  readonly format = input<'currency' | 'number'>('currency');

  protected readonly max = computed(() => Math.max(...this.points().map(point => point.value), 0));

  protected readonly bars = computed(() => {
    const max = this.max();
    const points = this.points();

    // With many bars there is no room for every label, so show every nth
    const labelEvery = Math.ceil(points.length / 10);

    return points.map((point, index) => ({
      label: point.label,
      value: point.value,
      height: max === 0 ? 0 : Math.round((point.value / max) * 100),
      showLabel: points.length <= 12 || index % labelEvery === 0
    }));
  });

  protected formatValue(value: number): string {
    return this.format() === 'currency'
      ? value.toLocaleString('en-US', { style: 'currency', currency: 'USD' })
      : value.toLocaleString('en-US');
  }
}
