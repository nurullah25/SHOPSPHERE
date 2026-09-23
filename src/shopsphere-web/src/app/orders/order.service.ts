import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { toHttpParams } from '../core/http/query-params';
import { PagedResult } from '../core/models/catalog.models';
import { CheckoutResult, Order, OrderSummary } from '../core/models/checkout.models';

@Injectable({ providedIn: 'root' })
export class OrderService {
  private readonly http = inject(HttpClient);

  getOrders(page = 1, pageSize = 10): Observable<PagedResult<OrderSummary>> {
    return this.http.get<PagedResult<OrderSummary>>('/api/orders', { params: toHttpParams({ page, pageSize }) });
  }

  getOrder(orderNumber: string): Observable<Order> {
    return this.http.get<Order>(`/api/orders/${orderNumber}`);
  }

  cancel(orderNumber: string, reason: string | null): Observable<Order> {
    return this.http.post<Order>(`/api/orders/${orderNumber}/cancel`, { reason });
  }

  retryPayment(orderNumber: string, paymentToken: string): Observable<CheckoutResult> {
    return this.http.post<CheckoutResult>(`/api/orders/${orderNumber}/payments`, { paymentToken });
  }
}
