import type { OrderStatus } from '@/features/orders/types';

export const STATUSES: OrderStatus[] = [
  'PendingPayment',
  'Paid',
  'Processing',
  'Shipped',
  'Delivered',
  'Cancelled',
  'Refunded',
  'Returned',
];

/** The admin order list URL contract (arch 13.2): one exact, case-sensitive status or All, and a whole page number >= 1. */
export function parseOrderListParams(raw: { status?: string | string[]; page?: string | string[] }): {
  status: OrderStatus | 'all';
  page: number;
} {
  const status = typeof raw.status === 'string' ? STATUSES.find((s) => s === raw.status) : undefined;
  const page = typeof raw.page === 'string' && /^\d+$/.test(raw.page) && Number(raw.page) >= 1 ? Number(raw.page) : 1;
  return { status: status ?? 'all', page };
}
