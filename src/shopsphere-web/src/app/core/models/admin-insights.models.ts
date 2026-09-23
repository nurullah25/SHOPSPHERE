export interface DailyRevenue {
  date: string;
  revenue: number;
  orders: number;
}

export interface RecentOrder {
  id: number;
  orderNumber: string;
  customerName: string;
  status: string;
  total: number;
  placedAt: string;
}

export interface LowStockProduct {
  productId: number;
  name: string;
  sku: string;
  stockQuantity: number;
  lowStockThreshold: number;
}

export interface Dashboard {
  periodDays: number;
  revenueInPeriod: number;
  revenuePreviousPeriod: number;
  ordersInPeriod: number;
  averageOrderValue: number;
  newCustomersInPeriod: number;
  revenueAllTime: number;
  totalOrders: number;
  totalCustomers: number;
  activeProducts: number;
  pendingOrders: number;
  lowStockProducts: number;
  revenueByDay: DailyRevenue[];
  recentOrders: RecentOrder[];
  lowStock: LowStockProduct[];
}

export interface ReportRange {
  from?: string | null;
  to?: string | null;
  groupBy?: 'day' | 'month';
  top?: number;
}

export interface SalesByPeriod {
  period: string;
  periodStart: string;
  revenue: number;
  orders: number;
  units: number;
}

export interface SalesByProduct {
  productId: number;
  productName: string;
  sku: string;
  unitsSold: number;
  revenue: number;
}

export interface SalesByCategory {
  categoryId: number;
  categoryName: string;
  unitsSold: number;
  revenue: number;
}

export interface OrderStatusSummary {
  status: string;
  orders: number;
  total: number;
}

export interface CustomerListItem {
  id: number;
  name: string;
  email: string;
  isActive: boolean;
  orderCount: number;
  totalSpent: number;
  lastOrderAt: string | null;
  createdAt: string;
}

export interface CustomerOrder {
  id: number;
  orderNumber: string;
  status: string;
  total: number;
  placedAt: string;
}

export interface CustomerDetail extends CustomerListItem {
  phoneNumber: string | null;
  lastLoginAt: string | null;
  recentOrders: CustomerOrder[];
}

export interface AdminCoupon {
  id: number;
  code: string;
  description: string | null;
  discountType: 'Percentage' | 'FixedAmount';
  discountValue: number;
  minOrderAmount: number | null;
  maxDiscountAmount: number | null;
  startsAt: string | null;
  expiresAt: string | null;
  usageLimit: number | null;
  usageLimitPerCustomer: number | null;
  timesUsed: number;
  isActive: boolean;
}

export interface CouponRequest {
  code: string;
  description: string | null;
  discountType: 'Percentage' | 'FixedAmount';
  discountValue: number;
  minOrderAmount: number | null;
  maxDiscountAmount: number | null;
  startsAt: string | null;
  expiresAt: string | null;
  usageLimit: number | null;
  usageLimitPerCustomer: number | null;
  isActive: boolean;
}
