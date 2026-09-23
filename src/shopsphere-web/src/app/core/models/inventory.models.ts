export interface InventoryItem {
  productId: number;
  name: string;
  sku: string;
  categoryName: string;
  stockQuantity: number;
  lowStockThreshold: number;
  reservedForPendingOrders: number;
  isLowStock: boolean;
  isActive: boolean;
  updatedAt: string;
}

export interface InventoryQuery {
  search?: string | null;
  categoryId?: number | null;
  lowStock?: boolean | null;
  sort: 'name' | 'stock_asc' | 'stock_desc';
  page: number;
  pageSize: number;
}

export interface StockAdjustmentRequest {
  quantityChange: number;
  reason: 'Restock' | 'Adjustment';
  note: string | null;
}

export interface InventoryMovement {
  id: number;
  quantityChange: number;
  quantityAfter: number;
  reason: string;
  note: string | null;
  orderNumber: string | null;
  changedBy: string | null;
  createdAt: string;
}
