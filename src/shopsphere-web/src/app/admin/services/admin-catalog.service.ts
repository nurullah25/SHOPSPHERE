import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { silentErrors } from '../../core/http/http-context';
import { toHttpParams } from '../../core/http/query-params';
import {
  AdminCategory,
  AdminProduct,
  AdminProductListItem,
  AdminProductQuery,
  CategoryRequest,
  CreateProductRequest,
  UpdateProductRequest
} from '../../core/models/admin-catalog.models';
import { PagedResult, ProductImage } from '../../core/models/catalog.models';

@Injectable({ providedIn: 'root' })
export class AdminCatalogService {
  private readonly http = inject(HttpClient);

  getProducts(query: AdminProductQuery): Observable<PagedResult<AdminProductListItem>> {
    return this.http.get<PagedResult<AdminProductListItem>>('/api/admin/products', { params: toHttpParams(query) });
  }

  getProduct(id: number): Observable<AdminProduct> {
    return this.http.get<AdminProduct>(`/api/admin/products/${id}`);
  }

  // Form saves show their errors inline, so the global toast is skipped
  createProduct(request: CreateProductRequest): Observable<AdminProduct> {
    return this.http.post<AdminProduct>('/api/admin/products', request, { context: silentErrors() });
  }

  updateProduct(id: number, request: UpdateProductRequest): Observable<AdminProduct> {
    return this.http.put<AdminProduct>(`/api/admin/products/${id}`, request, { context: silentErrors() });
  }

  deleteProduct(id: number): Observable<void> {
    return this.http.delete<void>(`/api/admin/products/${id}`);
  }

  uploadImage(productId: number, file: File): Observable<ProductImage> {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<ProductImage>(`/api/admin/products/${productId}/images`, form);
  }

  setMainImage(productId: number, imageId: number): Observable<void> {
    return this.http.put<void>(`/api/admin/products/${productId}/images/${imageId}/main`, null);
  }

  deleteImage(productId: number, imageId: number): Observable<void> {
    return this.http.delete<void>(`/api/admin/products/${productId}/images/${imageId}`);
  }

  getCategories(): Observable<AdminCategory[]> {
    return this.http.get<AdminCategory[]>('/api/admin/categories');
  }

  createCategory(request: CategoryRequest): Observable<AdminCategory> {
    return this.http.post<AdminCategory>('/api/admin/categories', request, { context: silentErrors() });
  }

  updateCategory(id: number, request: CategoryRequest): Observable<AdminCategory> {
    return this.http.put<AdminCategory>(`/api/admin/categories/${id}`, request, { context: silentErrors() });
  }

  deleteCategory(id: number): Observable<void> {
    return this.http.delete<void>(`/api/admin/categories/${id}`);
  }
}
