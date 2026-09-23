import { Component, computed, input } from '@angular/core';
import { CurrencyPipe } from '@angular/common';

@Component({
  selector: 'app-price',
  imports: [CurrencyPipe],
  template: `
    <span class="price" [class.large]="size() === 'large'">
      <span class="current" [class.sale]="onSale()">{{ effectivePrice() | currency: 'USD' }}</span>
      @if (onSale()) {
        <span class="original">{{ price() | currency: 'USD' }}</span>
        <span class="saving">-{{ savingPercent() }}%</span>
      }
    </span>
  `,
  styles: `
    .price {
      display: inline-flex;
      align-items: baseline;
      flex-wrap: wrap;
      gap: 6px;
    }

    .current {
      font: var(--mat-sys-title-medium);
      font-weight: 600;
    }

    .current.sale {
      color: var(--mat-sys-error);
    }

    .original {
      color: var(--mat-sys-on-surface-variant);
      text-decoration: line-through;
    }

    .saving {
      font: var(--mat-sys-label-small);
      color: var(--mat-sys-on-tertiary-container);
      background: var(--mat-sys-tertiary-container);
      padding: 2px 6px;
      border-radius: 4px;
    }

    .large .current {
      font: var(--mat-sys-headline-small);
      font-weight: 600;
    }
  `
})
export class Price {
  readonly price = input.required<number>();
  readonly discountPrice = input<number | null>(null);
  readonly size = input<'normal' | 'large'>('normal');

  protected readonly onSale = computed(() => {
    const discount = this.discountPrice();
    return discount !== null && discount < this.price();
  });

  protected readonly effectivePrice = computed(() => (this.onSale() ? this.discountPrice()! : this.price()));

  protected readonly savingPercent = computed(() =>
    Math.round((1 - this.effectivePrice() / this.price()) * 100)
  );
}
