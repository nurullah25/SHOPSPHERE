export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface CategoryNode {
  id: number;
  name: string;
  slug: string;
  description: string | null;
  children: CategoryNode[];
}

export interface CategoryLink {
  name: string;
  slug: string;
}

export type ProductSort = 'newest' | 'price_asc' | 'price_desc' | 'name' | 'rating';

export interface ProductQuery {
  search?: string;
  category?: string;
  minPrice?: number;
  maxPrice?: number;
  inStock?: boolean;
  sort: ProductSort;
  page: number;
  pageSize: number;
}

export interface ProductListItem {
  id: number;
  name: string;
  slug: string;
  price: number;
  discountPrice: number | null;
  effectivePrice: number;
  imageUrl: string | null;
  categoryName: string;
  categorySlug: string;
  inStock: boolean;
  lowStock: boolean;
  averageRating: number;
  reviewCount: number;
}

export interface ProductImage {
  id: number;
  url: string;
  altText: string | null;
  isMain: boolean;
}

export interface ProductDetail {
  id: number;
  name: string;
  slug: string;
  sku: string;
  description: string;
  price: number;
  discountPrice: number | null;
  effectivePrice: number;
  stockQuantity: number;
  lowStock: boolean;
  averageRating: number;
  reviewCount: number;
  images: ProductImage[];
  breadcrumb: CategoryLink[];
}
