import { PagedResult } from './catalog.models';

export interface Review {
  id: number;
  rating: number;
  title: string | null;
  comment: string | null;
  customerName: string;
  createdAt: string;
  updatedAt: string;
  isMine: boolean;
}

export interface ProductReviews {
  averageRating: number;
  reviewCount: number;
  ratingCounts: Record<number, number>;
  reviews: PagedResult<Review>;
  canReview: boolean;
  myReview: Review | null;
}

export interface ReviewRequest {
  rating: number;
  title: string | null;
  comment: string | null;
}
