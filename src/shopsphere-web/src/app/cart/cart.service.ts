import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { BehaviorSubject, Observable, catchError, map, of, tap } from 'rxjs';
import { AuthService } from '../core/auth/auth.service';
import { Cart, EMPTY_CART } from '../core/models/cart.models';

@Injectable({ providedIn: 'root' })
export class CartService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);

  private readonly cartSubject = new BehaviorSubject<Cart>(EMPTY_CART);

  readonly cart$ = this.cartSubject.asObservable();
  readonly itemCount$ = this.cart$.pipe(map(cart => cart.itemCount));

  constructor() {
    // The cart belongs to the signed-in customer, so it is loaded after login
    // and cleared on sign-out.
    this.auth.currentUser$.pipe(takeUntilDestroyed()).subscribe(user => {
      if (user?.role === 'Customer') {
        this.refresh();
      } else {
        this.cartSubject.next(EMPTY_CART);
      }
    });
  }

  get snapshot(): Cart {
    return this.cartSubject.value;
  }

  // Used when another feature (like the wishlist) gets an updated cart back
  applyCart(cart: Cart): void {
    this.cartSubject.next(cart);
  }

  refresh(): void {
    this.http
      .get<Cart>('/api/cart')
      .pipe(catchError(() => of(EMPTY_CART)))
      .subscribe(cart => this.cartSubject.next(cart));
  }

  addItem(productId: number, quantity = 1): Observable<Cart> {
    return this.http
      .post<Cart>('/api/cart/items', { productId, quantity })
      .pipe(tap(cart => this.cartSubject.next(cart)));
  }

  updateQuantity(productId: number, quantity: number): Observable<Cart> {
    return this.http
      .put<Cart>(`/api/cart/items/${productId}`, { quantity })
      .pipe(tap(cart => this.cartSubject.next(cart)));
  }

  removeItem(productId: number): Observable<Cart> {
    return this.http
      .delete<Cart>(`/api/cart/items/${productId}`)
      .pipe(tap(cart => this.cartSubject.next(cart)));
  }

  clear(): Observable<void> {
    return this.http.delete<void>('/api/cart').pipe(tap(() => this.cartSubject.next(EMPTY_CART)));
  }
}
