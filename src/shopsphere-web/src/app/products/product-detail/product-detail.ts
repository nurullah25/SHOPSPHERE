import { Component, computed, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Title } from '@angular/platform-browser';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDividerModule } from '@angular/material/divider';
import { MatIconModule } from '@angular/material/icon';
import { catchError, of, switchMap, tap } from 'rxjs';
import { CatalogService } from '../catalog.service';
import { ProductImage } from '../../core/models/catalog.models';
import { EmptyState } from '../../shared/components/empty-state/empty-state';
import { Price } from '../../shared/components/price/price';
import { ProductPhoto } from '../../shared/components/product-photo/product-photo';
import { RatingStars } from '../../shared/components/rating-stars/rating-stars';

@Component({
  selector: 'app-product-detail',
  imports: [RouterLink, MatButtonModule, MatDividerModule, MatIconModule, EmptyState, Price, ProductPhoto, RatingStars],
  templateUrl: './product-detail.html',
  styleUrl: './product-detail.scss'
})
export class ProductDetail {
  private readonly catalog = inject(CatalogService);
  private readonly title = inject(Title);

  // Bound from the :slug route parameter
  readonly slug = input.required<string>();

  protected readonly loading = signal(true);
  protected readonly notFound = signal(false);
  protected readonly selectedImageIndex = signal(0);

  protected readonly product = toSignal(
    toObservable(this.slug).pipe(
      tap(() => {
        this.loading.set(true);
        this.notFound.set(false);
        this.selectedImageIndex.set(0);
      }),
      switchMap(slug =>
        this.catalog.getProduct(slug).pipe(
          catchError(() => {
            this.notFound.set(true);
            return of(null);
          })
        )
      ),
      tap(product => {
        this.loading.set(false);
        this.title.setTitle(product ? `${product.name} | ShopSphere` : 'Product not found | ShopSphere');
      })
    )
  );

  protected readonly selectedImage = computed<ProductImage | null>(() => {
    const images = this.product()?.images ?? [];
    return images[this.selectedImageIndex()] ?? null;
  });
}
