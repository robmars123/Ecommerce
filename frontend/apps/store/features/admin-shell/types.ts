import type { OrderStatus } from '@/features/orders/types';

// Contract for GET /api/v1/admin/search?q= (decision log §13.2 "Admin search UI and contract"). No money field, by design.
export type AdminSearchOrder = { id: string; orderNumber: string; status: OrderStatus };
export type AdminSearchProduct = { id: string; name: string };
export type AdminSearchSku = { sku: string; productId: string; productName: string };
export type AdminSearchResult = {
  orders: AdminSearchOrder[];
  products: AdminSearchProduct[];
  skus: AdminSearchSku[];
};
