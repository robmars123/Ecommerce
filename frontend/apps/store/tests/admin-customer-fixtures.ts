import type { AdminCustomerDetail, AdminCustomerListItem, AdminCustomerListPage } from '@/features/admin-customers/types';

import { usd } from './admin-order-fixtures';

export function makeCustomerItem(overrides: Partial<AdminCustomerListItem> = {}): AdminCustomerListItem {
  return { id: 'c-1', email: 'ann@example.test', displayName: 'Ann Lee', createdAt: '2026-01-05T10:00:00Z', ...overrides };
}

export function makeCustomerPage(items: AdminCustomerListItem[], overrides: Partial<AdminCustomerListPage> = {}): AdminCustomerListPage {
  return { items, page: 1, pageSize: 20, totalCount: items.length, totalPages: items.length > 0 ? 1 : 0, ...overrides };
}

/** The Admin shape: totalSpent is set. */
export function makeCustomerDetail(overrides: Partial<AdminCustomerDetail> = {}): AdminCustomerDetail {
  return {
    id: 'c-1',
    email: 'ann@example.test',
    displayName: 'Ann Lee',
    createdAt: '2026-01-05T10:00:00Z',
    orderCount: 2,
    totalSpent: usd('110.00'),
    recentOrders: [
      { id: 'o-1', orderNumber: 'ORD-2026-000001', createdAt: '2026-03-10T10:00:00Z', status: 'Paid', grandTotal: usd('100.00') },
      { id: 'o-2', orderNumber: 'ORD-2026-000002', createdAt: '2026-03-11T10:00:00Z', status: 'Shipped', grandTotal: usd('40.00') },
    ],
    returnCount: 1,
    recentReturns: [
      { id: 'r-1', orderId: 'o-1', orderNumber: 'ORD-2026-000001', status: 'Requested', reason: 'Too small', createdAt: '2026-03-12T10:00:00Z', decidedAt: null },
    ],
    refundCount: 2,
    recentRefunds: [
      { id: 'rf-1', orderId: 'o-1', orderNumber: 'ORD-2026-000001', amount: usd('20.00'), status: 'Succeeded', automatic: true, createdAt: '2026-03-13T10:00:00Z' },
      { id: 'rf-2', orderId: 'o-2', orderNumber: 'ORD-2026-000002', amount: usd('5.00'), status: 'Pending', automatic: false, createdAt: '2026-03-14T10:00:00Z' },
    ],
    ...overrides,
  };
}
