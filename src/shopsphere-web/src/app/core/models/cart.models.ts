export interface CartItem {
  productId: number;
  name: string;
  slug: string;
  imageUrl: string | null;
  unitPrice: number;
  quantity: number;
  lineTotal: number;
  availableStock: number;
  isAvailable: boolean;
  issue: string | null;
}

export interface Cart {
  items: CartItem[];
  subtotal: number;
  itemCount: number;
  hasUnavailableItems: boolean;
}

export interface WishlistItem {
  productId: number;
  name: string;
  slug: string;
  imageUrl: string | null;
  price: number;
  discountPrice: number | null;
  inStock: boolean;
  isActive: boolean;
  addedAt: string;
}

export const EMPTY_CART: Cart = {
  items: [],
  subtotal: 0,
  itemCount: 0,
  hasUnavailableItems: false
};
