// Wire types for the admin dashboard API (arch 9, Slice 6b). Money is a string formatted by the server; the UI never adds it up.
import type { Money } from '@/features/catalog/types';

export type DashboardPeriod = 'today' | '7d' | '30d';

export type DashboardKpis = {
  revenue: Money;
  orderCount: number;
  averageOrderValue: Money;
  customers: { total: number; new: number; returning: number };
  /** Revenue over the same elapsed time one period earlier (server-computed). */
  previousRevenue: Money;
  /** Server percent change against previousRevenue, one decimal; null when previousRevenue is zero. */
  revenueChangePercent: number | null;
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
  /** Every low-stock variant: uncapped, unlike lowStock (which stops at 20). */
  lowStockCount: number;
  /** Orders cancelled within the period's window (distinct orders). */
  cancelledCount: number;
  /** Orders delivered within the period's window (distinct orders). */
  deliveredCount: number;
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

export type TrendBucket = { start: string; revenue: Money; orders: number };

/** Revenue over time, server-computed (P2-tier4): the buckets add up to `total`, which is the dashboard revenue. */
export type RevenueTrend = {
  period: DashboardPeriod;
  granularity: 'hour' | 'day';
  asOf: string;
  currency: string;
  total: Money;
  orderCount: number;
  buckets: TrendBucket[];
};
