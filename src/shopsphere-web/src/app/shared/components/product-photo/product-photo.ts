import { Component, input, signal } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

// Shows the product photo, or a neutral placeholder when there is no image
// or it fails to load.
@Component({
  selector: 'app-product-photo',
  imports: [MatIconModule],
  template: `
    @if (url() && !failed()) {
      <img [src]="url()" [alt]="alt()" loading="lazy" (error)="failed.set(true)" />
    } @else {
      <div class="placeholder" role="img" [attr.aria-label]="alt()">
        <mat-icon>chair</mat-icon>
      </div>
    }
  `,
  styles: `
    :host {
      display: block;
      aspect-ratio: 1;
      overflow: hidden;
      border-radius: 8px;
      background: var(--mat-sys-surface-container);
    }

    img {
      width: 100%;
      height: 100%;
      object-fit: cover;
    }

    .placeholder {
      width: 100%;
      height: 100%;
      display: flex;
      align-items: center;
      justify-content: center;
      color: var(--mat-sys-outline);

      mat-icon {
        font-size: 48px;
        width: 48px;
        height: 48px;
      }
    }
  `
})
export class ProductPhoto {
  readonly url = input<string | null>(null);
  readonly alt = input('');

  protected readonly failed = signal(false);
}
