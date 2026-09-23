export interface CheckoutItem {
  productId: number;
  name: string;
  slug: string;
  imageUrl: string | null;
  unitPrice: number;
  quantity: number;
  lineTotal: number;
}

export interface SavedAddress {
  id: number;
  fullName: string;
  line1: string;
  line2: string | null;
  city: string;
  state: string | null;
  postalCode: string;
  country: string;
  phoneNumber: string | null;
  isDefault: boolean;
}

export interface CheckoutSummary {
  items: CheckoutItem[];
  subtotal: number;
  discountAmount: number;
  couponCode: string | null;
  couponDescription: string | null;
  shippingCost: number;
  total: number;
  freeShippingThreshold: number;
  issues: string[];
  savedAddresses: SavedAddress[];
}

export interface ShippingAddressRequest {
  fullName: string;
  line1: string;
  line2: string | null;
  city: string;
  state: string | null;
  postalCode: string;
  country: string;
  phoneNumber: string | null;
  saveToAddressBook: boolean;
}

export interface PlaceOrderRequest {
  couponCode: string | null;
  addressId: number | null;
  shippingAddress: ShippingAddressRequest | null;
  paymentToken: string;
  notes: string | null;
}

export interface CheckoutResult {
  orderNumber: string;
  status: string;
  paymentStatus: string;
  total: number;
  paymentSucceeded: boolean;
  message: string;
}

export interface OrderItem {
  productId: number;
  productName: string;
  sku: string;
  slug: string | null;
  imageUrl: string | null;
  unitPrice: number;
  quantity: number;
  lineTotal: number;
}

export interface OrderStatusEntry {
  fromStatus: string | null;
  toStatus: string;
  note: string | null;
  changedAt: string;
}

export interface Order {
  orderNumber: string;
  status: string;
  placedAt: string;
  subtotal: number;
  discountAmount: number;
  couponCode: string | null;
  shippingCost: number;
  total: number;
  paymentStatus: string;
  paymentFailureReason: string | null;
  notes: string | null;
  cancellationReason: string | null;
  canCancel: boolean;
  items: OrderItem[];
  shippingAddress: SavedAddress;
  statusHistory: OrderStatusEntry[];
}

// The mock gateway accepts tokens only, so no card data reaches the API
export const PAYMENT_METHODS = [
  { token: 'tok_success', label: 'Visa ending 4242', hint: 'Payment approved' },
  { token: 'tok_declined', label: 'Visa ending 0002', hint: 'Card declined' },
  { token: 'tok_insufficient_funds', label: 'Mastercard ending 9995', hint: 'Insufficient funds' }
];

export interface OrderSummary {
  orderNumber: string;
  placedAt: string;
  status: string;
  paymentStatus: string;
  total: number;
  itemCount: number;
  previewItemName: string;
  previewImageUrl: string | null;
  canCancel: boolean;
}

export interface AdminOrderListItem {
  id: number;
  orderNumber: string;
  placedAt: string;
  status: string;
  paymentStatus: string;
  total: number;
  itemCount: number;
  customerName: string;
  customerEmail: string;
}

export interface AdminOrder extends Order {
  id: number;
  customerId: number;
  customerName: string;
  customerEmail: string;
  allowedNextStatuses: string[];
}
