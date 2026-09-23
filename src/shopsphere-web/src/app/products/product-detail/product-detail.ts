import { Component, computed, inject, input, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { Title } from '@angular/platform-browser';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDividerModule } from '@angular/material/divider';
import { MatIconModule } from '@angular/material/icon';
import { catchError, of, switchMap, tap } from 'rxjs';
import { CatalogService } from '../catalog.service';
import { ProductImage } from '../../core/models/catalog.models';
import { AuthService } from '../../core/auth/auth.service';
import { CartService } from '../../cart/cart.service';
import { WishlistService } from '../../wishlist/wishlist.service';
import { NotificationService } from '../../core/services/notification.service';
import { EmptyState } from '../../shared/components/empty-state/empty-state';
import { Price } from '../../shared/components/price/price';
import { ProductPhoto } from '../../shared/components/product-photo/product-photo';
import { QuantitySelector } from '../../shared/components/quantity-selector/quantity-selector';
import { RatingStars } from '../../shared/components/rating-stars/rating-stars';

@Component({
  selector: 'app-product-detail',
  imports: [
    RouterLink,
    MatButtonModule,
    MatDividerModule,
    MatIconModule,
    EmptyState,
    Price,
    ProductPhoto,
    QuantitySelector,
    RatingStars
  ],
  templateUrl: './product-detail.html',
  styleUrl: './product-detail.scss'
})
export class ProductDetail {
  private readonly catalog = inject(CatalogService);
  private readonly title = inject(Title);
  private readonly auth = inject(AuthService);
  private readonly cart = inject(CartService);
  private readonly wishlist = inject(WishlistService);
  private readonly notifications = inject(NotificationService);
  private readonly router = inject(Router);

  // Bound from the :slug route parameter
  readonly slug = input.required<string>();

  protected readonly loading = signal(true);
  protected readonly notFound = signal(false);
  protected readonly selectedImageIndex = signal(0);
  protected readonly quantity = signal(1);
  protected readonly addingToCart = signal(false);

  protected readonly product = toSignal(
    toObservable(this.slug).pipe(
      tap(() => {
        this.loading.set(true);
        this.notFound.set(false);
        this.selectedImageIndex.set(0);
        this.quantity.set(1);
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

  private readonly wishlistItems = toSignal(this.wishlist.items$, { initialValue: [] });

  protected readonly inWishlist = computed(() => {
    const id = this.product()?.id;
    return id !== undefined && this.wishlistItems().some(item => item.productId === id);
  });

  addToCart(): void {
    const product = this.product();
    if (!product || !this.requireCustomer()) return;

    this.addingToCart.set(true);
    this.cart.addItem(product.id, this.quantity()).subscribe({
      next: () => {
        this.addingToCart.set(false);
        this.notifications.success(`${product.name} was added to your cart.`);
      },
      error: () => this.addingToCart.set(false)
    });
  }

  toggleWishlist(): void {
    const product = this.product();
    if (!product || !this.requireCustomer()) return;

    const action = this.inWishlist() ? this.wishlist.remove(product.id) : this.wishlist.add(product.id);
    action.subscribe();
  }

  // Cart and wishlist belong to a customer account, so send guests to sign in
  // and bring them back to this product afterwards.
  private requireCustomer(): boolean {
    if (this.auth.currentUser?.role === 'Customer') {
      return true;
    }

    if (this.auth.isAdmin()) {
      this.notifications.error('Admin accounts cannot shop. Sign in with a customer account.');
      return false;
    }

    this.router.navigate(['/login'], { queryParams: { returnUrl: this.router.url } });
    return false;
  }
}
