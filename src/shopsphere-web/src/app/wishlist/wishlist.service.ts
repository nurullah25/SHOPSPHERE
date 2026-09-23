import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { BehaviorSubject, Observable, catchError, map, of, switchMap, tap } from 'rxjs';
import { AuthService } from '../core/auth/auth.service';
import { CartService } from '../cart/cart.service';
import { Cart, WishlistItem } from '../core/models/cart.models';

@Injectable({ providedIn: 'root' })
export class WishlistService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly cart = inject(CartService);

  private readonly itemsSubject = new BehaviorSubject<WishlistItem[]>([]);

  readonly items$ = this.itemsSubject.asObservable();
  readonly count$ = this.items$.pipe(map(items => items.length));

  constructor() {
    this.auth.currentUser$.pipe(takeUntilDestroyed()).subscribe(user => {
      if (user?.role === 'Customer') {
        this.refresh();
      } else {
        this.itemsSubject.next([]);
      }
    });
  }

  contains(productId: number): boolean {
    return this.itemsSubject.value.some(item => item.productId === productId);
  }

  refresh(): void {
    this.load().subscribe();
  }

  add(productId: number): Observable<WishlistItem[]> {
    return this.http.post(`/api/wishlist/${productId}`, null).pipe(switchMap(() => this.load()));
  }

  remove(productId: number): Observable<WishlistItem[]> {
    return this.http.delete(`/api/wishlist/${productId}`).pipe(switchMap(() => this.load()));
  }

  moveToCart(productId: number): Observable<Cart> {
    return this.http.post<Cart>(`/api/wishlist/${productId}/move-to-cart`, null).pipe(
      tap(cart => {
        // The API returns the updated cart, so the badge stays in sync
        this.cart.applyCart(cart);
        this.refresh();
      })
    );
  }

  private load(): Observable<WishlistItem[]> {
    return this.http.get<WishlistItem[]>('/api/wishlist').pipe(
      catchError(() => of([] as WishlistItem[])),
      tap(items => this.itemsSubject.next(items))
    );
  }
}
