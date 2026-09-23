import { Component, computed, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-quantity-selector',
  imports: [MatButtonModule, MatIconModule],
  template: `
    <div class="quantity">
      <button
        mat-icon-button
        type="button"
        [disabled]="disabled() || quantity() <= 1"
        (click)="change(quantity() - 1)"
        aria-label="Decrease quantity">
        <mat-icon>remove</mat-icon>
      </button>

      <span class="value">{{ quantity() }}</span>

      <button
        mat-icon-button
        type="button"
        [disabled]="disabled() || quantity() >= limit()"
        (click)="change(quantity() + 1)"
        aria-label="Increase quantity">
        <mat-icon>add</mat-icon>
      </button>
    </div>
  `,
  styles: `
    .quantity {
      display: inline-flex;
      align-items: center;
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: 24px;
    }

    .value {
      min-width: 32px;
      text-align: center;
      font-weight: 500;
    }
  `
})
export class QuantitySelector {
  readonly quantity = input.required<number>();
  readonly max = input<number>(99);
  readonly disabled = input(false);

  readonly quantityChange = output<number>();

  protected readonly limit = computed(() => Math.min(this.max(), 99));

  protected change(value: number): void {
    this.quantityChange.emit(value);
  }
}
