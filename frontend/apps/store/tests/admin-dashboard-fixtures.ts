import type { Dashboard, RecentOrder, RecentOrdersPage } from '@/features/admin-dashboard/types';

const usd = (amount: string) => ({ amount, currency: 'USD' });

export function makeDashboard(overrides: Partial<Dashboard> = {}): Dashboard {
  return {
    period: 'today',
    asOf: '2026-11-08T14:30:00Z',
    currency: 'USD',
    kpis: {
      revenue: usd('1234.50'),
      orderCount: 7,
      averageOrderValue: usd('176.36'),
      customers: { total: 5, new: 3, returning: 2 },
      previousRevenue: usd('1000.00'),
      revenueChangePercent: 23.5,
    },
    ordersByStatus: [
      { status: 'Paid', count: 4 },
      { status: 'Shipped', count: 2 },
    ],
    topProducts: [{ productId: 'p-1', name: 'Blue Shirt', units: 9 }],
    lowStock: [{ variantId: 'v-1', productName: 'Blue Shirt', sku: 'BS-M', available: 2 }],
    lowStockCount: 1,
    cancelledCount: 0,
    deliveredCount: 0,
    needsAttention: { pendingReturns: 0, deadLetters: [], laggingProjectors: [] },
    ...overrides,
  };
}

export function makeRecentOrder(overrides: Partial<RecentOrder> = {}): RecentOrder {
  return {
    id: 'o-1',
    orderNumber: 'ORD-2026-000001',
    customerEmail: 'a@example.test',
    createdAt: '2026-11-08T10:00:00Z',
    grandTotal: usd('45.00'),
    status: 'Paid',
    ...overrides,
  };
}

export function makeRecentOrdersPage(items: RecentOrder[], overrides: Partial<RecentOrdersPage> = {}): RecentOrdersPage {
  return { items, page: 1, pageSize: 10, totalCount: items.length, ...overrides };
}
