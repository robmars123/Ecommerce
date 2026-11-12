import type {
  AdminOrderDetail,
  AdminOrderListItem,
  AdminOrderListPage,
  ReturnListPage,
  ReturnRequestRow,
} from '@/features/admin-orders/types';

import { makeOrderDetail } from './order-fixtures';

export const usd = (amount: string) => ({ amount, currency: 'USD' });

export function makeAdminOrderDetail(overrides: Partial<AdminOrderDetail> = {}): AdminOrderDetail {
  return {
    order: makeOrderDetail({ id: 'o-9', status: 'Paid', grandTotal: usd('45.00') }),
    userId: 'u-42',
    refunds: [],
    refundedTotal: usd('0.00'),
    pendingTotal: usd('0.00'),
    remaining: usd('45.00'),
    returnRequests: [],
    ...overrides,
  };
}

export function makeReturnRow(overrides: Partial<ReturnRequestRow> = {}): ReturnRequestRow {
  return {
    id: 'r-1',
    orderId: 'o-9',
    orderNumber: 'ORD-2026-000009',
    status: 'Requested',
    reason: 'Too small',
    adminNote: null,
    items: [{ variantId: 'v-1', name: 'Black tee, M', quantity: 1 }],
    createdAt: '2026-03-10T10:00:00Z',
    decidedAt: null,
    ...overrides,
  };
}

export function makeReturnPage(items: ReturnRequestRow[], overrides: Partial<ReturnListPage> = {}): ReturnListPage {
  return { items, page: 1, pageSize: 20, totalCount: items.length, totalPages: items.length > 0 ? 1 : 0, ...overrides };
}

export function makeAdminListItem(overrides: Partial<AdminOrderListItem> = {}): AdminOrderListItem {
  return {
    id: 'o-1',
    orderNumber: 'ORD-2026-000001',
    customerEmail: 'a@example.test',
    createdAt: '2026-03-10T10:00:00Z',
    status: 'Paid',
    grandTotal: usd('25.00'),
    ...overrides,
  };
}

export function makeAdminListPage(items: AdminOrderListItem[], overrides: Partial<AdminOrderListPage> = {}): AdminOrderListPage {
  return { items, page: 1, pageSize: 20, totalCount: items.length, totalPages: items.length > 0 ? 1 : 0, ...overrides };
}
