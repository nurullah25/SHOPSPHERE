import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, shareReplay } from 'rxjs';
import { toHttpParams } from '../core/http/query-params';
import {
  CategoryNode,
  PagedResult,
  ProductDetail,
  ProductListItem,
  ProductQuery
} from '../core/models/catalog.models';

@Injectable({ providedIn: 'root' })
export class CatalogService {
  private readonly http = inject(HttpClient);

  // Categories rarely change, so the tree is loaded once and shared
  private readonly categoryTree$ = this.http
    .get<CategoryNode[]>('/api/categories')
    .pipe(shareReplay({ bufferSize: 1, refCount: false }));

  getCategoryTree(): Observable<CategoryNode[]> {
    return this.categoryTree$;
  }

  searchProducts(query: ProductQuery): Observable<PagedResult<ProductListItem>> {
    return this.http.get<PagedResult<ProductListItem>>('/api/products', { params: toHttpParams(query) });
  }

  getProduct(slug: string): Observable<ProductDetail> {
    return this.http.get<ProductDetail>(`/api/products/${encodeURIComponent(slug)}`);
  }
}
