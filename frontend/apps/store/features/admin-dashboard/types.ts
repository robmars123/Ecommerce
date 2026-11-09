// Wire types for the admin dashboard API (arch 9, Slice 6b). Money is a string formatted by the server; the UI never adds it up.
import type { Money } from '@/features/catalog/types';

export type DashboardPeriod = 'today' | '7d' | '30d';

export type DashboardKpis = {
  revenue: Money;
  orderCount: number;
  averageOrderValue: Money;
  customers: { total: number; new: number; returning: number };
};

export type Dashboard = {
  period: DashboardPeriod;
  /** The time the projected figures are true as of (arch 8.8.3). */
  asOf: string;
  currency: string;
  /** null for Support: money figures are for Admin only. */
  kpis: DashboardKpis | null;
  ordersByStatus: { status: string; count: number }[];
  topProducts: { productId: string; name: string; units: number }[];
  lowStock: { variantId: string; productName: string; sku: string; available: number }[];
  needsAttention: {
    pendingReturns: number;
    deadLetters: { consumer: string; count: number }[];
    laggingProjectors: { name: string; lagSeconds: number }[];
  };
};

export type RecentOrder = {
  id: string;
  orderNumber: string;
  customerEmail: string;
  createdAt: string;
  grandTotal: Money;
  status: string;
};

export type RecentOrdersPage = { items: RecentOrder[]; page: number; pageSize: number; totalCount: number };
