import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { silentErrors } from '../../core/http/http-context';
import { toHttpParams } from '../../core/http/query-params';
import { PagedResult } from '../../core/models/catalog.models';
import {
  AdminCoupon,
  CouponRequest,
  CustomerDetail,
  CustomerListItem,
  Dashboard,
  OrderStatusSummary,
  ReportRange,
  SalesByCategory,
  SalesByPeriod,
  SalesByProduct
} from '../../core/models/admin-insights.models';

@Injectable({ providedIn: 'root' })
export class AdminInsightsService {
  private readonly http = inject(HttpClient);

  getDashboard(days = 30): Observable<Dashboard> {
    return this.http.get<Dashboard>('/api/admin/dashboard', { params: toHttpParams({ days }) });
  }

  getSalesByDate(range: ReportRange): Observable<SalesByPeriod[]> {
    return this.http.get<SalesByPeriod[]>('/api/admin/reports/sales-by-date', { params: toHttpParams(range) });
  }

  getSalesByProduct(range: ReportRange): Observable<SalesByProduct[]> {
    return this.http.get<SalesByProduct[]>('/api/admin/reports/sales-by-product', { params: toHttpParams(range) });
  }

  getSalesByCategory(range: ReportRange): Observable<SalesByCategory[]> {
    return this.http.get<SalesByCategory[]>('/api/admin/reports/sales-by-category', { params: toHttpParams(range) });
  }

  getOrderStatusSummary(range: ReportRange): Observable<OrderStatusSummary[]> {
    return this.http.get<OrderStatusSummary[]>('/api/admin/reports/order-status', { params: toHttpParams(range) });
  }

  getCustomers(search: string | null, isActive: boolean | null, page: number, pageSize: number): Observable<PagedResult<CustomerListItem>> {
    return this.http.get<PagedResult<CustomerListItem>>('/api/admin/customers', {
      params: toHttpParams({ search, isActive, page, pageSize })
    });
  }

  getCustomer(id: number): Observable<CustomerDetail> {
    return this.http.get<CustomerDetail>(`/api/admin/customers/${id}`);
  }

  setCustomerStatus(id: number, isActive: boolean): Observable<void> {
    return this.http.patch<void>(`/api/admin/customers/${id}/status`, { isActive });
  }

  getCoupons(): Observable<AdminCoupon[]> {
    return this.http.get<AdminCoupon[]>('/api/admin/coupons');
  }

  createCoupon(request: CouponRequest): Observable<AdminCoupon> {
    return this.http.post<AdminCoupon>('/api/admin/coupons', request, { context: silentErrors() });
  }

  updateCoupon(id: number, request: CouponRequest): Observable<AdminCoupon> {
    return this.http.put<AdminCoupon>(`/api/admin/coupons/${id}`, request, { context: silentErrors() });
  }

  deleteCoupon(id: number): Observable<void> {
    return this.http.delete<void>(`/api/admin/coupons/${id}`);
  }
}
