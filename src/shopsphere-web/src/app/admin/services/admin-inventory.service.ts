import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { silentErrors } from '../../core/http/http-context';
import { toHttpParams } from '../../core/http/query-params';
import { PagedResult } from '../../core/models/catalog.models';
import { InventoryItem, InventoryMovement, InventoryQuery, StockAdjustmentRequest } from '../../core/models/inventory.models';

@Injectable({ providedIn: 'root' })
export class AdminInventoryService {
  private readonly http = inject(HttpClient);

  getInventory(query: InventoryQuery): Observable<PagedResult<InventoryItem>> {
    return this.http.get<PagedResult<InventoryItem>>('/api/admin/inventory', { params: toHttpParams(query) });
  }

  adjustStock(productId: number, request: StockAdjustmentRequest): Observable<InventoryMovement> {
    return this.http.post<InventoryMovement>(`/api/admin/inventory/${productId}/adjustments`, request, {
      context: silentErrors()
    });
  }

  getMovements(productId: number, page = 1, pageSize = 20): Observable<PagedResult<InventoryMovement>> {
    return this.http.get<PagedResult<InventoryMovement>>(`/api/admin/inventory/${productId}/movements`, {
      params: toHttpParams({ page, pageSize })
    });
  }
}
