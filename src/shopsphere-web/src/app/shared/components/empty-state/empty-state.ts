import { Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-empty-state',
  imports: [MatIconModule],
  template: `
    <div class="empty-state">
      <mat-icon>{{ icon() }}</mat-icon>
      <h2>{{ title() }}</h2>
      @if (message()) {
        <p>{{ message() }}</p>
      }
      <ng-content />
    </div>
  `,
  styles: `
    .empty-state {
      display: flex;
      flex-direction: column;
      align-items: center;
      text-align: center;
      padding: 48px 16px;
      color: var(--mat-sys-on-surface-variant);
    }

    mat-icon {
      font-size: 48px;
      width: 48px;
      height: 48px;
      color: var(--mat-sys-outline);
    }

    h2 {
      margin: 12px 0 4px;
      font: var(--mat-sys-title-large);
      color: var(--mat-sys-on-surface);
    }

    p {
      margin: 0 0 16px;
    }
  `
})
export class EmptyState {
  readonly icon = input('inventory_2');
  readonly title = input.required<string>();
  readonly message = input<string>();
}
