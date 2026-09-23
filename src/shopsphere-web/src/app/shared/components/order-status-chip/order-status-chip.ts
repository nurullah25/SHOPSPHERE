import { Component, input } from '@angular/core';

@Component({
  selector: 'app-order-status-chip',
  template: `<span class="chip" [class]="status().toLowerCase()">{{ status() }}</span>`,
  styles: `
    .chip {
      display: inline-block;
      padding: 2px 10px;
      border-radius: 12px;
      font: var(--mat-sys-label-medium);
      background: var(--mat-sys-surface-container-high);
      color: var(--mat-sys-on-surface-variant);
    }

    .confirmed,
    .processing {
      background: var(--mat-sys-secondary-container);
      color: var(--mat-sys-on-secondary-container);
    }

    .shipped {
      background: var(--mat-sys-tertiary-container);
      color: var(--mat-sys-on-tertiary-container);
    }

    .delivered {
      background: #e3f4e5;
      color: #1b5e20;
    }

    .cancelled {
      background: var(--mat-sys-error-container);
      color: var(--mat-sys-on-error-container);
    }
  `
})
export class OrderStatusChip {
  readonly status = input.required<string>();
}
