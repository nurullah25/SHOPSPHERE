import { Component, computed, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-rating-stars',
  imports: [MatIconModule],
  template: `
    <span class="rating" [attr.aria-label]="rating() + ' out of 5 stars'">
      @for (star of stars(); track $index) {
        <mat-icon>{{ star }}</mat-icon>
      }
      @if (count() !== null) {
        <span class="count">({{ count() }})</span>
      }
    </span>
  `,
  styles: `
    .rating {
      display: inline-flex;
      align-items: center;
      color: #f5a623;
    }

    mat-icon {
      font-size: 18px;
      width: 18px;
      height: 18px;
    }

    .count {
      margin-left: 4px;
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }
  `
})
export class RatingStars {
  readonly rating = input.required<number>();
  readonly count = input<number | null>(null);

  protected readonly stars = computed(() => {
    const rating = this.rating();
    return [1, 2, 3, 4, 5].map(position => {
      if (rating >= position) return 'star';
      if (rating >= position - 0.5) return 'star_half';
      return 'star_border';
    });
  });
}
