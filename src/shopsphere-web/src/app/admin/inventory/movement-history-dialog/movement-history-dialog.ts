import { Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { finalize } from 'rxjs';
import { AdminInventoryService } from '../../services/admin-inventory.service';
import { InventoryItem, InventoryMovement } from '../../../core/models/inventory.models';

@Component({
  selector: 'app-movement-history-dialog',
  imports: [DatePipe, MatDialogModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>Stock history</h2>

    <mat-dialog-content>
      <p class="muted">{{ item.name }} &middot; SKU {{ item.sku }}</p>

      @if (movements().length === 0 && !loading()) {
        <p>No stock movements recorded yet.</p>
      }

      <ul class="movements">
        @for (movement of movements(); track movement.id) {
          <li>
            <span class="change" [class.negative]="movement.quantityChange < 0">
              {{ movement.quantityChange > 0 ? '+' : '' }}{{ movement.quantityChange }}
            </span>
            <span class="details">
              <strong>{{ movement.reason }}</strong>
              <span class="muted">
                {{ movement.createdAt | date: 'short' }} &middot; stock after {{ movement.quantityAfter }}
                @if (movement.orderNumber) {
                  &middot; {{ movement.orderNumber }}
                }
                @if (movement.changedBy) {
                  &middot; {{ movement.changedBy }}
                }
              </span>
              @if (movement.note) {
                <span class="muted">{{ movement.note }}</span>
              }
            </span>
          </li>
        }
      </ul>
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Close</button>
    </mat-dialog-actions>
  `,
  styles: `
    mat-dialog-content {
      min-width: 380px;
    }

    .movements {
      list-style: none;
      margin: 0;
      padding: 0;

      li {
        display: flex;
        gap: 12px;
        padding: 8px 0;
        border-bottom: 1px solid var(--mat-sys-outline-variant);
      }
    }

    .change {
      min-width: 44px;
      font: var(--mat-sys-title-medium);
      color: #2e7d32;

      &.negative {
        color: var(--mat-sys-error);
      }
    }

    .details {
      display: flex;
      flex-direction: column;
    }

    .muted {
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }
  `
})
export class MovementHistoryDialog {
  private readonly inventory = inject(AdminInventoryService);

  protected readonly item = inject<InventoryItem>(MAT_DIALOG_DATA);
  protected readonly movements = signal<InventoryMovement[]>([]);
  protected readonly loading = signal(true);

  constructor() {
    this.inventory
      .getMovements(this.item.productId, 1, 50)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe(result => this.movements.set(result.items));
  }
}
