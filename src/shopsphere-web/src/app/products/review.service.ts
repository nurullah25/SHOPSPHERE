import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { silentErrors } from '../core/http/http-context';
import { toHttpParams } from '../core/http/query-params';
import { ProductReviews, Review, ReviewRequest } from '../core/models/review.models';

@Injectable({ providedIn: 'root' })
export class ReviewService {
  private readonly http = inject(HttpClient);

  getReviews(productId: number, page = 1, pageSize = 5): Observable<ProductReviews> {
    return this.http.get<ProductReviews>(`/api/products/${productId}/reviews`, {
      params: toHttpParams({ page, pageSize })
    });
  }

  create(productId: number, request: ReviewRequest): Observable<Review> {
    return this.http.post<Review>(`/api/products/${productId}/reviews`, request, { context: silentErrors() });
  }

  update(reviewId: number, request: ReviewRequest): Observable<Review> {
    return this.http.put<Review>(`/api/reviews/${reviewId}`, request, { context: silentErrors() });
  }

  delete(reviewId: number): Observable<void> {
    return this.http.delete<void>(`/api/reviews/${reviewId}`);
  }
}
