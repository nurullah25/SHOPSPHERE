import { ProductImage } from './catalog.models';

export type AdminProductSort = 'newest' | 'name' | 'price_asc' | 'price_desc' | 'stock_asc';

export interface AdminProductQuery {
  search?: string;
  categoryId?: number;
  isActive?: boolean;
  lowStock?: boolean;
  sort: AdminProductSort;
  page: number;
  pageSize: number;
}

export interface AdminProductListItem {
  id: number;
  name: string;
  sku: string;
  slug: string;
  categoryName: string;
  price: number;
  discountPrice: number | null;
  stockQuantity: number;
  lowStockThreshold: number;
  isActive: boolean;
  imageUrl: string | null;
  updatedAt: string;
}

export interface AdminProduct {
  id: number;
  name: string;
  slug: string;
  sku: string;
  description: string;
  price: number;
  discountPrice: number | null;
  categoryId: number;
  stockQuantity: number;
  lowStockThreshold: number;
  isActive: boolean;
  images: ProductImage[];
  createdAt: string;
  updatedAt: string;
  rowVersion: string;
}

export interface ProductRequest {
  name: string;
  slug: string | null;
  sku: string;
  description: string;
  price: number;
  discountPrice: number | null;
  categoryId: number;
  lowStockThreshold: number;
  isActive: boolean;
}

export interface CreateProductRequest extends ProductRequest {
  stockQuantity: number;
}

export interface UpdateProductRequest extends ProductRequest {
  rowVersion: string;
}

export interface AdminCategory {
  id: number;
  name: string;
  slug: string;
  description: string | null;
  parentId: number | null;
  sortOrder: number;
  isActive: boolean;
  productCount: number;
  children: AdminCategory[];
}

export interface CategoryRequest {
  name: string;
  slug: string | null;
  description: string | null;
  parentId: number | null;
  sortOrder: number;
  isActive: boolean;
}
