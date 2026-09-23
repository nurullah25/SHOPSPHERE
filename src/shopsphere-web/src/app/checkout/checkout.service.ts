import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { silentErrors } from '../core/http/http-context';
import { CheckoutResult, CheckoutSummary, Order, PlaceOrderRequest } from '../core/models/checkout.models';

@Injectable({ providedIn: 'root' })
export class CheckoutService {
  private readonly http = inject(HttpClient);

  getSummary(couponCode: string | null = null): Observable<CheckoutSummary> {
    return this.http.post<CheckoutSummary>('/api/checkout/summary', { couponCode });
  }

  // The coupon field shows its own error message next to the input
  validateCoupon(code: string): Observable<CheckoutSummary> {
    return this.http.post<CheckoutSummary>('/api/coupons/validate', { code }, { context: silentErrors() });
  }

  placeOrder(request: PlaceOrderRequest): Observable<CheckoutResult> {
    return this.http.post<CheckoutResult>('/api/checkout', request, { context: silentErrors() });
  }

  getOrder(orderNumber: string): Observable<Order> {
    return this.http.get<Order>(`/api/orders/${orderNumber}`);
  }

  retryPayment(orderNumber: string, paymentToken: string): Observable<CheckoutResult> {
    return this.http.post<CheckoutResult>(`/api/orders/${orderNumber}/payments`, { paymentToken });
  }
}
