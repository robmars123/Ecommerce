import type { StockStatus } from '@/features/catalog/types';

export type AdminInventoryItem = {
  variantId: string;
  productName: string;
  sku: string;
  variantName: string;
  isActive: boolean;
  onHand: number;
  reserved: number;
  available: number;
  stockStatus: StockStatus;
};

export type AdminInventoryPage = {
  items: AdminInventoryItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
};

export type AdjustStockRequest = {
  variantId: string;
  delta: number;
  expectedOnHand: number;
  reason: string;
};
