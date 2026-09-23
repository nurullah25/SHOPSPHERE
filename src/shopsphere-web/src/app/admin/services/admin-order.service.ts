import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { toHttpParams } from '../../core/http/query-params';
import { PagedResult } from '../../core/models/catalog.models';
import { AdminOrder, AdminOrderListItem } from '../../core/models/checkout.models';

export interface AdminOrderQuery {
  status?: string | null;
  search?: string | null;
  from?: string | null;
  to?: string | null;
  page: number;
  pageSize: number;
}

@Injectable({ providedIn: 'root' })
export class AdminOrderService {
  private readonly http = inject(HttpClient);

  getOrders(query: AdminOrderQuery): Observable<PagedResult<AdminOrderListItem>> {
    return this.http.get<PagedResult<AdminOrderListItem>>('/api/admin/orders', { params: toHttpParams(query) });
  }

  getOrder(id: number): Observable<AdminOrder> {
    return this.http.get<AdminOrder>(`/api/admin/orders/${id}`);
  }

  changeStatus(id: number, status: string, note: string | null): Observable<AdminOrder> {
    return this.http.post<AdminOrder>(`/api/admin/orders/${id}/status`, { status, note });
  }
}
